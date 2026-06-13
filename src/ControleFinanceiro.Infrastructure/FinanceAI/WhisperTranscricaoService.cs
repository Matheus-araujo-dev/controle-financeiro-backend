using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.FinanceAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Infrastructure.FinanceAI;

public sealed class WhisperTranscricaoService(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options,
    ILogger<WhisperTranscricaoService> logger) : ITranscricaoAudioService
{
    private static readonly Dictionary<string, string> MimeToExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/ogg"] = "ogg",
        ["audio/mpeg"] = "mp3",
        ["audio/mp4"] = "m4a",
        ["audio/webm"] = "webm",
        ["audio/wav"] = "wav",
        ["audio/x-wav"] = "wav",
    };

    public async Task<string?> TranscreverAsync(string midiaBase64, string mimeType, CancellationToken cancellationToken)
    {
        try
        {
            var opts = options.Value;
            var audioBytes = Convert.FromBase64String(midiaBase64);
            var extension = MimeToExtension.GetValueOrDefault(mimeType, "ogg");

            using var form = new MultipartFormDataContent();

            var fileContent = new ByteArrayContent(audioBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mimeType);
            form.Add(fileContent, "file", $"audio.{extension}");
            form.Add(new StringContent(opts.WhisperModel), "model");

            if (!string.IsNullOrWhiteSpace(opts.WhisperLanguage))
                form.Add(new StringContent(opts.WhisperLanguage), "language");

            using var response = await httpClient.PostAsync("audio/transcriptions", form, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Whisper API erro {Status}: {Body}", (int)response.StatusCode, error);
                return null;
            }

            var node = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
            return node?["text"]?.GetValue<string>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao transcrever áudio via Whisper.");
            return null;
        }
    }
}
