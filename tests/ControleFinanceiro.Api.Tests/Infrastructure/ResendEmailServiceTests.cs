using ControleFinanceiro.Infrastructure.Alertas;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Api.Tests.Infrastructure;

public sealed class ResendEmailServiceTests
{
    private static ResendEmailService BuildService(bool enabled, string apiKey = "")
    {
        var opts = Options.Create(new ResendOptions
        {
            Enabled = enabled,
            ApiKey = apiKey,
            FromEmail = "noreply@test.com",
            FromName = "Testes"
        });
        // HttpClient nunca será chamado quando Enabled=false ou ApiKey vazia
        var http = new HttpClient { BaseAddress = new Uri("https://api.resend.com/") };
        return new ResendEmailService(http, opts, NullLogger<ResendEmailService>.Instance);
    }

    [Fact]
    public async Task EnviarAsync_QuandoDesativado_RetornaFalseSemChamarHttp()
    {
        var servico = BuildService(enabled: false, apiKey: "key-xyz");

        var result = await servico.EnviarAsync("dest@test.com", "Assunto", "<p>Body</p>", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task EnviarAsync_QuandoApiKeyVazia_RetornaFalseSemChamarHttp()
    {
        var servico = BuildService(enabled: true, apiKey: "");

        var result = await servico.EnviarAsync("dest@test.com", "Assunto", "<p>Body</p>", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task EnviarAsync_QuandoHttpRetornaErro_RetornaFalse()
    {
        var opts = Options.Create(new ResendOptions
        {
            Enabled = true,
            ApiKey = "key-invalida",
            FromEmail = "noreply@test.com",
            FromName = "Testes"
        });

        // Handler que simula resposta 401 do Resend
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"Unauthorized\"}")
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var servico = new ResendEmailService(http, opts, NullLogger<ResendEmailService>.Instance);

        var result = await servico.EnviarAsync("dest@test.com", "Assunto", "<p>Body</p>", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task EnviarAsync_QuandoHttpLancaExcecao_RetornaFalse()
    {
        var opts = Options.Create(new ResendOptions
        {
            Enabled = true,
            ApiKey = "key-invalida",
            FromEmail = "noreply@test.com",
            FromName = "Testes"
        });

        // Handler que lança exceção simulando falha de rede
        var handler = new FakeHttpMessageHandler(new InvalidOperationException("Rede indisponível"));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var servico = new ResendEmailService(http, opts, NullLogger<ResendEmailService>.Instance);

        var result = await servico.EnviarAsync("dest@test.com", "Assunto", "<p>Body</p>", CancellationToken.None);

        result.Should().BeFalse();
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage? _response;
        private readonly Exception? _exception;

        public FakeHttpMessageHandler(HttpResponseMessage response) => _response = response;
        public FakeHttpMessageHandler(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_exception is not null) throw _exception;
            return Task.FromResult(_response!);
        }
    }
}
