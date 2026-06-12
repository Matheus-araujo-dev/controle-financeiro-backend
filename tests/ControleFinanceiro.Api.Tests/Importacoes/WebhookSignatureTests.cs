using System.Net;
using System.Security.Cryptography;
using System.Text;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ControleFinanceiro.Api.Tests.Importacoes;

public sealed class WebhookSignatureTests : IDisposable
{
    private const string Secret = "segredo-webhook-de-teste";
    private const string WebhookUrl = "/api/v1/importacoes-whatsapp/webhook";

    private const string PayloadJson =
        """{"tipoOrigem":"Texto","remetente":"+5511999999999","textoBruto":"Paguei 50 reais de mercado"}""";

    private readonly CustomWebApplicationFactory _factory = new();
    private readonly WebApplicationFactory<Program> _signedFactory;

    public WebhookSignatureTests()
    {
        _signedFactory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Whatsapp:WebhookSecret", Secret));
    }

    public void Dispose()
    {
        _signedFactory.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Webhook_SemAssinatura_DeveRetornar401()
    {
        using var client = _signedFactory.CreateClient();

        var response = await client.PostAsync(WebhookUrl, JsonContent(PayloadJson));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Webhook_ComAssinaturaInvalida_DeveRetornar401()
    {
        using var client = _signedFactory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = JsonContent(PayloadJson)
        };
        request.Headers.Add("X-Webhook-Signature", $"sha256={new string('0', 64)}");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Webhook_ComAssinaturaValida_DeveProcessar()
    {
        using var client = _signedFactory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = JsonContent(PayloadJson)
        };
        request.Headers.Add("X-Webhook-Signature", $"sha256={ComputeSignature(PayloadJson)}");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Webhook_ComHeaderMeta_DeveProcessar()
    {
        using var client = _signedFactory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = JsonContent(PayloadJson)
        };
        request.Headers.Add("X-Hub-Signature-256", $"sha256={ComputeSignature(PayloadJson)}");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Webhook_SemSecretConfigurado_DeveContinuarAceitandoForaDeProducao()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync(WebhookUrl, JsonContent(PayloadJson));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static StringContent JsonContent(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static string ComputeSignature(string payload)
    {
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Secret),
            Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
