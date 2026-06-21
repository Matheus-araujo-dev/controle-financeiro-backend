using System.Net;
using ControleFinanceiro.Infrastructure.FinanceAI;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.CircuitBreaker;

namespace ControleFinanceiro.Infrastructure.Tests.FinanceAI;

public sealed class HttpResiliencePoliciesTests
{
    private static HttpResponseMessage Resposta(HttpStatusCode status) => new(status);

    [Fact]
    public async Task RetryPolicy_DeveRepetirEmErroTransitorioEEntaoSucesso()
    {
        var policy = HttpResiliencePolicies.RetryPolicy(NullLogger.Instance, "teste");
        var tentativas = 0;

        var resposta = await policy.ExecuteAsync(() =>
        {
            tentativas++;
            return Task.FromResult(Resposta(tentativas < 3 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
        });

        tentativas.Should().Be(3); // 1 inicial + 2 retentativas
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RetryPolicy_DeveDesistirAposTresRetentativas()
    {
        var policy = HttpResiliencePolicies.RetryPolicy(NullLogger.Instance, "teste");
        var tentativas = 0;

        var resposta = await policy.ExecuteAsync(() =>
        {
            tentativas++;
            return Task.FromResult(Resposta(HttpStatusCode.InternalServerError));
        });

        tentativas.Should().Be(4); // 1 inicial + 3 retentativas
        resposta.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CircuitBreaker_DeveAbrirAposCincoFalhas()
    {
        var policy = HttpResiliencePolicies.CircuitBreakerPolicy(NullLogger.Instance, "teste");

        for (var i = 0; i < 5; i++)
        {
            await policy.ExecuteAsync(() => Task.FromResult(Resposta(HttpStatusCode.ServiceUnavailable)));
        }

        var acao = async () => await policy.ExecuteAsync(() => Task.FromResult(Resposta(HttpStatusCode.OK)));

        await acao.Should().ThrowAsync<BrokenCircuitException>();
    }
}
