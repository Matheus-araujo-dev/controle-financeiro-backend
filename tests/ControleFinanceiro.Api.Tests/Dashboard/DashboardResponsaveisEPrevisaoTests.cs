using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Dashboard;

// Cobre caminhos do DashboardAppService que agregam/ordenam por decimal — só testáveis em SQL Server
// real (LocalDB), não no SQLite: ranking por responsável e central de previsão.
public sealed class DashboardResponsaveisEPrevisaoTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static async Task SeedLancamentosAsync(HttpClient client, FinancialFixtureSeed.FixtureIds fixture)
    {
        await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-03",
            responsavelCompraId = fixture.ResponsavelId,
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 300m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Gasto responsável",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 300m } }
        });

        await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-03",
            responsavelId = fixture.ResponsavelId,
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-22",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 500m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Receita responsável",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 500m } }
        });
    }

    [Fact]
    public async Task ResponsaveisResumo_DeveRetornarRankingComDados()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        await SeedLancamentosAsync(client, fixture);

        var resposta = await client.GetAsync("/api/v1/dashboard/responsaveis/resumo?mesReferencia=2026-04");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("itens").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CentralPrevisaoResumoEItens_DeveExecutar()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        await SeedLancamentosAsync(client, fixture);

        var resumo = await client.GetAsync("/api/v1/dashboard/central-previsao/resumo?mesReferencia=2026-04");
        resumo.StatusCode.Should().Be(HttpStatusCode.OK);
        var parseResumo = () => JsonDocument.Parse(resumo.Content.ReadAsStringAsync().Result);
        parseResumo.Should().NotThrow();

        var itens = await client.GetAsync("/api/v1/dashboard/central-previsao/itens?mesReferencia=2026-04");
        itens.StatusCode.Should().Be(HttpStatusCode.OK);
        var parseItens = () => JsonDocument.Parse(itens.Content.ReadAsStringAsync().Result);
        parseItens.Should().NotThrow();
    }

    [Fact]
    public async Task ContasGerenciaisResumo_ComTipoReceita_DeveExecutar()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        await SeedLancamentosAsync(client, fixture);

        var resposta = await client.GetAsync("/api/v1/dashboard/contas-gerenciais/resumo?mesReferencia=2026-04&tipo=Receita");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var parse = () => JsonDocument.Parse(resposta.Content.ReadAsStringAsync().Result);
        parse.Should().NotThrow();
    }
}
