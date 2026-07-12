using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ControleFinanceiro.Application.Common.Alertas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DomainPushSubscription = ControleFinanceiro.Domain.FinanceAI.PushSubscription;

namespace ControleFinanceiro.Infrastructure.Alertas;

public sealed class WebPushService(
    HttpClient http,
    IOptions<VapidOptions> options,
    ILogger<WebPushService> logger) : IPushAlertaService
{
    public string GetVapidPublicKey() => options.Value.PublicKey;

    public async Task<bool> EnviarAsync(
        DomainPushSubscription subscription,
        string titulo,
        string corpo,
        CancellationToken cancellationToken)
    {
        var opts = options.Value;
        if (!opts.Enabled || string.IsNullOrWhiteSpace(opts.PublicKey) || string.IsNullOrWhiteSpace(opts.PrivateKey))
        {
            logger.LogDebug("VAPID desativado. Push para endpoint {Ep} não enviado.", subscription.Endpoint);
            return false;
        }

        var payload = JsonSerializer.Serialize(new { title = titulo, body = corpo });
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        try
        {
            // Encrypt payload using Web Push encryption (RFC 8291)
            var encrypted = EncryptPayload(payloadBytes, subscription.P256dh, subscription.Auth);
            var vapidJwt = CreateVapidJwt(subscription.Endpoint, opts);

            using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Endpoint);
            request.Content = new ByteArrayContent(encrypted);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content.Headers.ContentEncoding.Add("aes128gcm");
            request.Headers.TryAddWithoutValidation("Authorization", $"vapid t={vapidJwt},k={opts.PublicKey}");
            request.Headers.TryAddWithoutValidation("TTL", "86400");

            var response = await http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Gone || response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogInformation("Push subscription expirada: {Endpoint}", subscription.Endpoint);
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Push retornou {Status} para {Endpoint}", (int)response.StatusCode, subscription.Endpoint);
                return false;
            }

            logger.LogInformation("Push enviado → {Endpoint}: {Titulo}", subscription.Endpoint, titulo);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao enviar push para {Endpoint}.", subscription.Endpoint);
            return false;
        }
    }

    private static byte[] EncryptPayload(byte[] payload, string p256dhBase64Url, string authBase64Url)
    {
        var p256dhBytes = Base64UrlDecode(p256dhBase64Url);
        var authBytes = Base64UrlDecode(authBase64Url);

        // Generate server EC key pair
        using var serverEcKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var serverPublicKey = serverEcKey.PublicKey.ExportSubjectPublicKeyInfo();

        // Import client public key
        using var clientEcKey = ECDiffieHellman.Create();
        clientEcKey.ImportSubjectPublicKeyInfo(p256dhBytes, out _);

        // Derive shared secret
        var sharedSecret = serverEcKey.DeriveKeyMaterial(clientEcKey.PublicKey);

        // Derive keys using HKDF (RFC 8291)
        var salt = RandomNumberGenerator.GetBytes(16);
        var serverPublicKeyBytes = ExportRawPublicKey(serverEcKey);

        // PRK = HKDF-Extract(auth, shared_secret)
        var prk = HKDF.Extract(HashAlgorithmName.SHA256, sharedSecret, authBytes);

        // CEK and nonce info
        var cekInfo = BuildInfo("aesgcm", p256dhBytes, serverPublicKeyBytes);
        var nonceInfo = BuildInfo("nonce", p256dhBytes, serverPublicKeyBytes);

        var cek = HKDF.Expand(HashAlgorithmName.SHA256, prk, 16, cekInfo);
        var nonce = HKDF.Expand(HashAlgorithmName.SHA256, prk, 12, nonceInfo);

        // Encrypt using AES-128-GCM
        using var aes = new AesGcm(cek, AesGcm.TagByteSizes.MaxSize);
        var ciphertext = new byte[payload.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        aes.Encrypt(nonce, payload, ciphertext, tag);

        // Build aes128gcm content: salt(16) + record_size(4) + keyid_len(1) + keyid + ciphertext + tag
        var rs = BitConverter.GetBytes((uint)(payload.Length + 65 + 16 + 1));
        if (BitConverter.IsLittleEndian) Array.Reverse(rs);

        var result = new byte[16 + 4 + 1 + 65 + ciphertext.Length + tag.Length];
        int pos = 0;
        salt.CopyTo(result, pos); pos += 16;
        rs.CopyTo(result, pos); pos += 4;
        result[pos++] = 65;
        serverPublicKeyBytes.CopyTo(result, pos); pos += 65;
        ciphertext.CopyTo(result, pos); pos += ciphertext.Length;
        tag.CopyTo(result, pos);

        return result;
    }

    private static byte[] ExportRawPublicKey(ECDiffieHellman ecKey)
    {
        var params_ = ecKey.ExportParameters(false);
        var x = params_.Q.X!;
        var y = params_.Q.Y!;
        var raw = new byte[65];
        raw[0] = 0x04;
        x.CopyTo(raw, 1);
        y.CopyTo(raw, 33);
        return raw;
    }

    private static byte[] BuildInfo(string type, byte[] clientPublicKey, byte[] serverPublicKey)
    {
        // "Content-Encoding: {type}\0P-256\0" + uint16 client key len + client key + uint16 server key len + server key
        var typeBytes = Encoding.ASCII.GetBytes($"Content-Encoding: {type}\0P-256\0");
        var result = new byte[typeBytes.Length + 2 + clientPublicKey.Length + 2 + serverPublicKey.Length];
        int pos = 0;
        typeBytes.CopyTo(result, pos); pos += typeBytes.Length;
        result[pos++] = (byte)(clientPublicKey.Length >> 8);
        result[pos++] = (byte)(clientPublicKey.Length & 0xff);
        clientPublicKey.CopyTo(result, pos); pos += clientPublicKey.Length;
        result[pos++] = (byte)(serverPublicKey.Length >> 8);
        result[pos++] = (byte)(serverPublicKey.Length & 0xff);
        serverPublicKey.CopyTo(result, pos);
        return result;
    }

    private static string CreateVapidJwt(string endpoint, VapidOptions opts)
    {
        var uri = new Uri(endpoint);
        var audience = $"{uri.Scheme}://{uri.Host}";
        var expiry = DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds();

        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new { typ = "JWT", alg = "ES256" }));
        var claims = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            aud = audience,
            exp = expiry,
            sub = opts.Subject
        }));

        var signingInput = $"{header}.{claims}";
        var privateKeyBytes = Base64UrlDecode(opts.PrivateKey);

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportECPrivateKey(privateKeyBytes, out _);

        var signature = ecdsa.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256);
        return $"{signingInput}.{Base64UrlEncode(signature)}";
    }

    private static byte[] Base64UrlDecode(string value)
    {
        value = value.Replace('-', '+').Replace('_', '/');
        switch (value.Length % 4)
        {
            case 2: value += "=="; break;
            case 3: value += "="; break;
        }
        return Convert.FromBase64String(value);
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
