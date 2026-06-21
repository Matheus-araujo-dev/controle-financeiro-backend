using System.Net.Http.Json;
using System.Text.Json;
using ControleFinanceiro.Api.BackgroundServices;
using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControleFinanceiro.Api.Tests.BackgroundServices;

public sealed class BackgroundWorkersTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task AtualizacaoStatusContasWorker_DeveMarcarContaVencidaNaPrimeiraIteracao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var resposta = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2020-01-01",
            recebedorId = fixture.RecebedorId,
            dataVencimento = DateTime.Today.AddDays(-3).ToString("yyyy-MM-dd"),
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 70m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Worker vencida",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 70m } }
        });
        resposta.EnsureSuccessStatusCode();
        var id = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var worker = new AtualizacaoStatusContasWorker(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AtualizacaoStatusContasWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await PollUntilAsync(async () => await ObterStatusAsync(client, id) == "VENCIDA");
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        (await ObterStatusAsync(client, id)).Should().Be("VENCIDA");
    }

    [Fact]
    public async Task RecorrenciaMensalWorker_DeveRodarPrimeiraIteracaoSemErros()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        var worker = new RecorrenciaMensalWorker(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<RecorrenciaMensalWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        // Tempo para a primeira iteração (geração idempotente; sem regras ativas é no-op).
        await Task.Delay(1000);
        var parar = async () => await worker.StopAsync(CancellationToken.None);

        await parar.Should().NotThrowAsync();
    }

    private async Task<string?> ObterStatusAsync(HttpClient client, Guid id)
    {
        var detalhe = await client.GetFromJsonAsync<JsonElement>($"/api/v1/contas-pagar/{id}", JsonOptions);
        return detalhe.GetProperty("statusCodigo").GetString();
    }

    private static async Task PollUntilAsync(Func<Task<bool>> condicao)
    {
        for (var tentativa = 0; tentativa < 50; tentativa++)
        {
            if (await condicao())
            {
                return;
            }

            await Task.Delay(200);
        }
    }
}
