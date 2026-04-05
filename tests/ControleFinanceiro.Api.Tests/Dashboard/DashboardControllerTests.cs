using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Dashboard;

public sealed class DashboardControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task GetResumo_DeveConsolidarCardsListasMovimentosERisco()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var despesaLiquidadaId = await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-04-01",
            dataVencimento: "2026-04-02",
            valor: 100m,
            descricao: "Despesa liquidada");

        await LiquidarContaPagarAsync(client, despesaLiquidadaId, fixture.ContaBancariaId, "2026-04-02");

        var receitaLiquidadaId = await CriarContaReceberAsync(
            client,
            fixture,
            dataEmissao: "2026-04-03",
            dataVencimento: "2026-04-04",
            valor: 250m,
            descricao: "Receita recebida");

        await LiquidarContaReceberAsync(client, receitaLiquidadaId, fixture.ContaBancariaId, "2026-04-04");

        await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-04-01",
            dataVencimento: "2026-04-03",
            valor: 800m,
            descricao: "Fornecedor atrasado");

        await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-04-04",
            dataVencimento: "2026-04-08",
            valor: 700m,
            descricao: "Imposto da semana");

        await CriarContaReceberAsync(
            client,
            fixture,
            dataEmissao: "2026-04-04",
            dataVencimento: "2026-04-09",
            valor: 200m,
            descricao: "Cliente da semana");

        var resumo = await client.GetFromJsonAsync<DashboardResumoResponse>(
            "/api/v1/dashboard/resumo?dataReferencia=2026-04-05&diasProjetados=10");

        resumo.Should().NotBeNull();
        resumo!.SaldoAtual.Should().Be(1150m);
        resumo.TotalAPagar.Should().Be(1500m);
        resumo.TotalAReceber.Should().Be(200m);
        resumo.SaldoProjetado.Should().Be(-150m);
        resumo.RiscoSaldoNegativo.Should().BeTrue();
        resumo.ContasVencidas.Should().ContainSingle(item =>
            item.Descricao == "Fornecedor atrasado" &&
            item.TipoLancamento == "ContaPagar" &&
            item.StatusCodigo == "VENCIDA");
        resumo.ContasAVencer.Should().Contain(item =>
            item.Descricao == "Imposto da semana" &&
            item.TipoLancamento == "ContaPagar");
        resumo.ContasAVencer.Should().Contain(item =>
            item.Descricao == "Cliente da semana" &&
            item.TipoLancamento == "ContaReceber");
        resumo.MovimentacoesRecentes.Should().HaveCountGreaterThanOrEqualTo(2);
        resumo.MovimentacoesRecentes.First().DataMovimentacao.Should().Be(new DateOnly(2026, 4, 4));
        resumo.MovimentacoesRecentes.Should().Contain(item =>
            item.ContaReceberId == receitaLiquidadaId &&
            item.Tipo == "Entrada" &&
            item.Natureza == "Realizada" &&
            item.Valor == 250m);
        resumo.MovimentacoesRecentes.Should().Contain(item =>
            item.ContaPagarId == despesaLiquidadaId &&
            item.Tipo == "Saida" &&
            item.Natureza == "Realizada" &&
            item.Valor == 100m);
    }

    [Fact]
    public async Task GetFluxoCaixa_DeveDiferenciarVisaoCaixaEEconomicaParaCompraNoCartao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        await CriarCompraCartaoAsync(
            client,
            fixture,
            dataEmissao: "2026-04-05",
            valor: 300m,
            descricao: "Notebook no cartao");

        var fluxoCaixa = await client.GetFromJsonAsync<DashboardFluxoCaixaResponse>(
            "/api/v1/dashboard/fluxo-caixa?dataInicial=2026-04-05&dias=20&visao=Caixa");

        var fluxoEconomico = await client.GetFromJsonAsync<DashboardFluxoCaixaResponse>(
            "/api/v1/dashboard/fluxo-caixa?dataInicial=2026-04-05&dias=20&visao=Economica");

        fluxoCaixa.Should().NotBeNull();
        fluxoEconomico.Should().NotBeNull();
        fluxoCaixa!.Visao.Should().Be("Caixa");
        fluxoEconomico!.Visao.Should().Be("Economica");

        var caixaDiaCompra = fluxoCaixa.Itens.Single(item => item.Data == new DateOnly(2026, 4, 5));
        var economicoDiaCompra = fluxoEconomico.Itens.Single(item => item.Data == new DateOnly(2026, 4, 5));
        var caixaDiaVencimento = fluxoCaixa.Itens.Single(item => item.Data == new DateOnly(2026, 4, 20));
        var economicoDiaVencimento = fluxoEconomico.Itens.Single(item => item.Data == new DateOnly(2026, 4, 20));

        caixaDiaCompra.SaidasPrevistas.Should().Be(0m);
        economicoDiaCompra.SaidasPrevistas.Should().Be(300m);
        economicoDiaCompra.SaldoFinalPrevisto.Should().Be(700m);

        caixaDiaVencimento.SaidasPrevistas.Should().Be(300m);
        caixaDiaVencimento.SaldoFinalPrevisto.Should().Be(700m);
        economicoDiaVencimento.SaidasPrevistas.Should().Be(0m);
    }

    private static async Task<Guid> CriarContaPagarAsync(
        HttpClient client,
        FinancialFixtureSeed.FixtureIds fixture,
        string dataEmissao,
        string dataVencimento,
        decimal valor,
        string descricao)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao,
            recebedorId = fixture.RecebedorId,
            dataVencimento,
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = valor,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao,
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private static async Task<Guid> CriarContaReceberAsync(
        HttpClient client,
        FinancialFixtureSeed.FixtureIds fixture,
        string dataEmissao,
        string dataVencimento,
        decimal valor,
        string descricao)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao,
            pagadorId = fixture.PagadorId,
            dataVencimento,
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = valor,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao,
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private static async Task<Guid> CriarCompraCartaoAsync(
        HttpClient client,
        FinancialFixtureSeed.FixtureIds fixture,
        string dataEmissao,
        decimal valor,
        string descricao)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao,
            responsavelCompraId = fixture.ResponsavelId,
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoCartaoId,
            cartaoId = fixture.CartaoId,
            contaBancariaId = (string?)null,
            valorOriginal = valor,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao,
            observacao = "Compra via cartao",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private static async Task LiquidarContaPagarAsync(
        HttpClient client,
        Guid contaPagarId,
        Guid contaBancariaId,
        string dataLiquidacao)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{contaPagarId}/liquidar", new
        {
            dataLiquidacao,
            contaBancariaId
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task LiquidarContaReceberAsync(
        HttpClient client,
        Guid contaReceberId,
        Guid contaBancariaId,
        string dataLiquidacao)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/contas-receber/{contaReceberId}/liquidar", new
        {
            dataLiquidacao,
            contaBancariaId
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed record IdResponse(Guid Id);

    private sealed record DashboardResumoResponse(
        decimal SaldoAtual,
        decimal TotalAPagar,
        decimal TotalAReceber,
        decimal SaldoProjetado,
        bool RiscoSaldoNegativo,
        IReadOnlyCollection<DashboardContaResumoResponse> ContasVencidas,
        IReadOnlyCollection<DashboardContaResumoResponse> ContasAVencer,
        IReadOnlyCollection<DashboardMovimentacaoResumoResponse> MovimentacoesRecentes);

    private sealed record DashboardContaResumoResponse(
        Guid Id,
        string TipoLancamento,
        string Descricao,
        string PessoaNome,
        DateOnly DataVencimento,
        decimal Valor,
        string StatusCodigo,
        string StatusNome);

    private sealed record DashboardMovimentacaoResumoResponse(
        Guid Id,
        DateOnly DataMovimentacao,
        string Tipo,
        string Natureza,
        decimal Valor,
        string? Observacao,
        Guid? ContaPagarId,
        Guid? ContaReceberId,
        Guid? FaturaCartaoId);

    private sealed record DashboardFluxoCaixaResponse(
        string Visao,
        DateOnly DataInicial,
        int Dias,
        bool RiscoSaldoNegativo,
        IReadOnlyCollection<DashboardFluxoCaixaDiaResponse> Itens);

    private sealed record DashboardFluxoCaixaDiaResponse(
        DateOnly Data,
        decimal SaldoInicial,
        decimal EntradasPrevistas,
        decimal SaidasPrevistas,
        decimal SaldoFinalPrevisto,
        bool RiscoSaldoNegativo);
}
