using System.Net;
using System.Text;
using ControleFinanceiro.Infrastructure.FinanceAI;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Infrastructure.Tests.FinanceAI;

public sealed class WhisperTranscricaoServiceTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static WhisperTranscricaoService CriarServico(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") },
            Options.Create(new OpenAiOptions()),
            NullLogger<WhisperTranscricaoService>.Instance);

    private static string AudioBase64() => Convert.ToBase64String([1, 2, 3, 4]);

    [Fact]
    public async Task TranscreverAsync_ComSucesso_DeveRetornarTextoEChamarEndpointCorreto()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"text":"olá mundo"}""");
        var servico = CriarServico(handler);

        var texto = await servico.TranscreverAsync(AudioBase64(), "audio/ogg", CancellationToken.None);

        texto.Should().Be("olá mundo");
        handler.RequestUri.Should().EndWith("audio/transcriptions");
    }

    [Fact]
    public async Task TranscreverAsync_ComErroHttp_DeveRetornarNull()
    {
        var servico = CriarServico(new StubHandler(HttpStatusCode.BadRequest, "erro"));

        var texto = await servico.TranscreverAsync(AudioBase64(), "audio/mpeg", CancellationToken.None);

        texto.Should().BeNull();
    }

    [Fact]
    public async Task TranscreverAsync_ComBase64Invalido_DeveRetornarNull()
    {
        var servico = CriarServico(new StubHandler(HttpStatusCode.OK, """{"text":"x"}"""));

        var texto = await servico.TranscreverAsync("@@@invalido@@@", "audio/ogg", CancellationToken.None);

        texto.Should().BeNull();
    }
}
