using System.Net.Http.Json;
using System.Text.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Financeiro.Status;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class AtualizacaoStatusContasTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string Vencido() => DateTime.Today.AddDays(-5).ToString("yyyy-MM-dd");

    private static async Task<Guid> CriarContaPagarVencidaAsync(HttpClient client, FinancialFixtureSeed.FixtureIds fixture)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2020-01-01",
            recebedorId = fixture.RecebedorId,
            dataVencimento = Vencido(),
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 120m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta vencida pagar",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 120m } }
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CriarContaReceberVencidaAsync(HttpClient client, FinancialFixtureSeed.FixtureIds fixture)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2020-01-01",
            pagadorId = fixture.PagadorId,
            dataVencimento = Vencido(),
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 90m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta vencida receber",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 90m } }
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<string?> ObterStatusAsync(HttpClient client, string rota, Guid id)
    {
        var detalhe = await client.GetFromJsonAsync<JsonElement>($"{rota}/{id}", JsonOptions);
        return detalhe.GetProperty("statusCodigo").GetString();
    }

    [Fact]
    public async Task Listar_ContaPagarVencida_DeveExibirStatusVencidaSemPersistir()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarContaPagarVencidaAsync(client, fixture);

        var lista = await client.GetFromJsonAsync<JsonElement>("/api/v1/contas-pagar?pageSize=50", JsonOptions);
        var item = lista.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id);

        // Status efetivo na listagem mesmo antes do worker rodar.
        item.GetProperty("statusCodigo").GetString().Should().Be("VENCIDA");
    }

    [Fact]
    public async Task MarcarContasVencidas_DevePersistirVencidaParaPagarEReceber()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var contaPagarId = await CriarContaPagarVencidaAsync(client, fixture);
        var contaReceberId = await CriarContaReceberVencidaAsync(client, fixture);

        int atualizadas;
        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<AtualizacaoStatusContasService>();
            atualizadas = await service.MarcarContasVencidasAsync(CancellationToken.None);
        }

        atualizadas.Should().BeGreaterThanOrEqualTo(2);
        (await ObterStatusAsync(client, "/api/v1/contas-pagar", contaPagarId)).Should().Be("VENCIDA");
        (await ObterStatusAsync(client, "/api/v1/contas-receber", contaReceberId)).Should().Be("VENCIDA");
    }
}
