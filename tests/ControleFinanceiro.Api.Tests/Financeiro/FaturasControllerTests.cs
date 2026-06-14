using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class FaturasControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task GetEPostPagar_DeveAgruparComprasExibirItensEGerarSaidaRealDaFatura()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var compraAbrilA = await CriarCompraCartaoAsync(client, fixture, "2026-04-05", 100m, "Compra cartao abril A");
        var compraAbrilB = await CriarCompraCartaoAsync(client, fixture, "2026-04-09", 50m, "Compra cartao abril B");
        await CriarCompraCartaoAsync(client, fixture, "2026-04-11", 75m, "Compra cartao maio");

        var movimentosAntesPagamento = await client.GetFromJsonAsync<PagedResponse<MovimentacaoResumoResponse>>("/api/v1/movimentacoes");
        var faturas = await client.GetFromJsonAsync<FaturaListResponse>("/api/v1/faturas");

        movimentosAntesPagamento.Should().NotBeNull();
        movimentosAntesPagamento!.Items.Should().NotContain(item =>
            item.ContaPagarId == compraAbrilA ||
            item.ContaPagarId == compraAbrilB);

        faturas.Should().NotBeNull();
        faturas!.Items.Should().HaveCount(2);

        var faturaAbril = faturas.Items.Single(item => item.Competencia == "2026-04");
        faturaAbril.ValorTotal.Should().Be(150m);
        faturaAbril.QuantidadeItens.Should().Be(2);
        faturaAbril.StatusCodigo.Should().Be("ABERTA");

        var detalheAntesPagamento = await client.GetFromJsonAsync<FaturaDetalheResponse>($"/api/v1/faturas/{faturaAbril.Id}");
        detalheAntesPagamento.Should().NotBeNull();
        detalheAntesPagamento!.Itens.Should().HaveCount(2);
        detalheAntesPagamento.Itens.Should().OnlyContain(item => item.StatusCodigo == "EM_FATURA");

        var pagarResponse = await client.PostAsJsonAsync($"/api/v1/faturas/{faturaAbril.Id}/pagar", new
        {
            dataPagamento = "2026-04-20",
            contaBancariaPagamentoId = fixture.ContaBancariaId,
            observacao = "Pagamento da fatura abril"
        });

        var detalheDepoisPagamento = await client.GetFromJsonAsync<FaturaDetalheResponse>($"/api/v1/faturas/{faturaAbril.Id}");
        var movimentosDepoisPagamento = await client.GetFromJsonAsync<PagedResponse<MovimentacaoResumoResponse>>("/api/v1/movimentacoes");

        pagarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        detalheDepoisPagamento.Should().NotBeNull();
        detalheDepoisPagamento!.StatusCodigo.Should().Be("PAGA");
        detalheDepoisPagamento.DataPagamento.Should().Be(new DateOnly(2026, 4, 20));
        detalheDepoisPagamento.Itens.Should().OnlyContain(item => item.StatusCodigo == "LIQUIDADA");

        movimentosDepoisPagamento.Should().NotBeNull();
        movimentosDepoisPagamento!.Items.Should().ContainSingle(item =>
            item.FaturaCartaoId == faturaAbril.Id &&
            item.Tipo == "Saida" &&
            item.Natureza == "Realizada" &&
            item.ContaBancariaId == fixture.ContaBancariaId &&
            item.Valor == 150m);
    }

    [Fact]
    public async Task EstornarPagamento_DeveReabrirFaturaEPermitirNovoPagamento()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        await CriarCompraCartaoAsync(client, fixture, "2026-04-05", 100m, "Compra cartao abril A");
        await CriarCompraCartaoAsync(client, fixture, "2026-04-09", 50m, "Compra cartao abril B");

        var faturas = await client.GetFromJsonAsync<FaturaListResponse>("/api/v1/faturas");
        var faturaAbril = faturas!.Items.Single(item => item.Competencia == "2026-04");

        var pagarResponse = await client.PostAsJsonAsync($"/api/v1/faturas/{faturaAbril.Id}/pagar", new
        {
            dataPagamento = "2026-04-20",
            contaBancariaPagamentoId = fixture.ContaBancariaId,
            observacao = "Pagamento da fatura abril"
        });

        pagarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var estornarResponse = await client.PostAsync($"/api/v1/faturas/{faturaAbril.Id}/estornar", null);
        var detalheEstornado = await estornarResponse.Content.ReadFromJsonAsync<FaturaDetalheResponse>();

        estornarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        detalheEstornado.Should().NotBeNull();
        detalheEstornado!.StatusCodigo.Should().Be("ABERTA");
        detalheEstornado.DataPagamento.Should().BeNull();
        detalheEstornado.Itens.Should().OnlyContain(item => item.StatusCodigo == "EM_FATURA");

        var pagarNovamenteResponse = await client.PostAsJsonAsync($"/api/v1/faturas/{faturaAbril.Id}/pagar", new
        {
            dataPagamento = "2026-04-21",
            contaBancariaPagamentoId = fixture.ContaBancariaId,
            observacao = "Pagamento refeito da fatura abril"
        });

        pagarNovamenteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostEstornar_DeveReabrirFaturaECancelarSaidaReal()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        await CriarCompraCartaoAsync(client, fixture, "2026-04-05", 100m, "Compra cartao abril A");
        await CriarCompraCartaoAsync(client, fixture, "2026-04-09", 50m, "Compra cartao abril B");

        var faturas = await client.GetFromJsonAsync<FaturaListResponse>("/api/v1/faturas");
        var faturaAbril = faturas!.Items.Single(item => item.Competencia == "2026-04");

        var pagarResponse = await client.PostAsJsonAsync($"/api/v1/faturas/{faturaAbril.Id}/pagar", new
        {
            dataPagamento = "2026-04-20",
            contaBancariaPagamentoId = fixture.ContaBancariaId,
            observacao = "Pagamento da fatura abril"
        });

        pagarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var estornarResponse = await client.PostAsync($"/api/v1/faturas/{faturaAbril.Id}/estornar", null);
        estornarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detalheDepoisEstorno = await client.GetFromJsonAsync<FaturaDetalheResponse>($"/api/v1/faturas/{faturaAbril.Id}");

        detalheDepoisEstorno.Should().NotBeNull();
        detalheDepoisEstorno!.StatusCodigo.Should().Be("ABERTA");
        detalheDepoisEstorno.DataPagamento.Should().BeNull();
        detalheDepoisEstorno.ContaBancariaPagamentoId.Should().BeNull();
        detalheDepoisEstorno.Itens.Should().OnlyContain(item => item.StatusCodigo == "EM_FATURA");

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        dbContext.MovimentacoesFinanceiras.Count(x =>
                x.FaturaCartaoId == faturaAbril.Id &&
                x.StatusMovimentacaoId != StatusMovimentacao.CanceladaId)
            .Should().Be(0);
    }

    [Fact]
    public async Task Get_DeveOrdenarPorCartaoEExporResumoFiltradoPorCartaoECompetencia()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var cartaoSecundarioId = await CriarCartaoAsync(client, "A Cartao", "Visa", "7777", 10, 20);

        await CriarCompraCartaoAsync(client, fixture, "2026-04-05", 150m, "Compra cartao principal", fixture.CartaoId);
        await CriarCompraCartaoAsync(client, fixture, "2026-04-05", 200m, "Compra cartao secundario", cartaoSecundarioId);

        var ordenadoPorCartao = await client.GetFromJsonAsync<FaturaListResponse>(
            "/api/v1/faturas?sortBy=cartaoNome&sortDirection=Asc");

        ordenadoPorCartao.Should().NotBeNull();
        ordenadoPorCartao!.Items.Should().HaveCount(2);
        ordenadoPorCartao.Items.First().CartaoNome.Should().Be("A Cartao");

        var filtrado = await client.GetFromJsonAsync<FaturaListResponse>(
            $"/api/v1/faturas?cartaoId={cartaoSecundarioId}&competencia=2026-04&statusCodigo=ABERTA&dataVencimentoInicial=2026-04-01&dataVencimentoFinal=2026-04-30&dataFechamentoInicial=2026-04-01&dataFechamentoFinal=2026-04-30");

        filtrado.Should().NotBeNull();
        filtrado!.Items.Should().ContainSingle();
        filtrado.Summary.TotalRegistros.Should().Be(1);
        filtrado.Summary.ValorTotal.Should().Be(200m);
        filtrado.Summary.PorCartao.Should().ContainSingle();
        filtrado.Summary.PorCartao.Single().Label.Should().Be("A Cartao");
        filtrado.Summary.PorCartao.Single().ValorTotal.Should().Be(200m);
        filtrado.Summary.PorCompetencia.Should().ContainSingle();
        filtrado.Summary.PorCompetencia.Single().Label.Should().Be("2026-04");
        filtrado.Summary.PorCompetencia.Single().ValorTotal.Should().Be(200m);
    }

    [Fact]
    public async Task PostImportarConfirmar_DeveCriarContaComRateioEDeduplicarPelaChave()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var payload = new
        {
            cartaoId = fixture.CartaoId,
            formaPagamentoId = fixture.FormaPagamentoCartaoId,
            recebedorPadraoId = fixture.RecebedorId,
            contaGerencialPadraoId = fixture.ContaGerencialDespesaId,
            itens = new[]
            {
                new
                {
                    dataTransacao = "2026-04-05",
                    descricao = "Mercado importado",
                    valor = 123.45m,
                    chaveImportacao = "2026-04-05|mercado-importado|123.45"
                }
            }
        };

        var primeiraConfirmacao = await client.PostAsJsonAsync("/api/v1/faturas/importar/confirmar", payload);
        var resultadoPrimeiraConfirmacao = await primeiraConfirmacao.Content.ReadFromJsonAsync<ConfirmarImportacaoFaturaResponse>();

        primeiraConfirmacao.StatusCode.Should().Be(HttpStatusCode.OK);
        resultadoPrimeiraConfirmacao.Should().NotBeNull();
        resultadoPrimeiraConfirmacao!.ContasCriadas.Should().Be(1);
        resultadoPrimeiraConfirmacao.ContasDuplicadas.Should().Be(0);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var contaCriada = await dbContext.ContasPagar
            .SingleAsync(x => x.ChaveSerieImportacaoCartao == $"{fixture.CartaoId}|2026-04-05|mercado-importado|123.45");
        var rateio = await dbContext.RateiosContaGerencial.SingleAsync(x => x.ContaPagarId == contaCriada.Id);

        contaCriada.Origem.Should().Be(OrigemLancamento.Importacao);
        contaCriada.StatusContaId.Should().Be(StatusConta.EmFaturaId);
        rateio.ContaGerencialId.Should().Be(fixture.ContaGerencialDespesaId);
        rateio.Valor.Should().Be(123.45m);

        var segundaConfirmacao = await client.PostAsJsonAsync("/api/v1/faturas/importar/confirmar", payload);
        var resultadoSegundaConfirmacao = await segundaConfirmacao.Content.ReadFromJsonAsync<ConfirmarImportacaoFaturaResponse>();

        segundaConfirmacao.StatusCode.Should().Be(HttpStatusCode.OK);
        resultadoSegundaConfirmacao.Should().NotBeNull();
        resultadoSegundaConfirmacao!.ContasCriadas.Should().Be(0);
        resultadoSegundaConfirmacao.ContasDuplicadas.Should().Be(1);
    }

    private static async Task<Guid> CriarCompraCartaoAsync(
        HttpClient client,
        FinancialFixtureSeed.FixtureIds fixture,
        string dataEmissao,
        decimal valor,
        string descricao,
        Guid? cartaoId = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            numeroDocumento = "CARTAO-01",
            dataEmissao,
            responsavelCompraId = fixture.ResponsavelId,
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoCartaoId,
            cartaoId = cartaoId ?? fixture.CartaoId,
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

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ContaDetalheResponse>();
        return payload!.Id;
    }

    private static async Task<Guid> CriarCartaoAsync(
        HttpClient client,
        string nome,
        string bandeira,
        string numeroFinal,
        int diaFechamentoFatura,
        int diaVencimentoFatura)
    {
        var response = await client.PostAsJsonAsync("/api/v1/cartoes", new
        {
            nome,
            bandeira,
            numeroFinal,
            diaFechamentoFatura,
            diaVencimentoFatura,
            ativo = true
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ContaDetalheResponse>();
        return payload!.Id;
    }

    private sealed record ContaDetalheResponse(Guid Id);

    private sealed record FaturaAgrupamentoResumoResponse(
        string Chave,
        string Label,
        int QuantidadeFaturas,
        decimal ValorTotal);

    private sealed record FaturaListSummaryResponse(
        int TotalRegistros,
        decimal ValorTotal,
        IReadOnlyCollection<FaturaAgrupamentoResumoResponse> PorCartao,
        IReadOnlyCollection<FaturaAgrupamentoResumoResponse> PorCompetencia);

    private sealed record FaturaListResponse(
        IReadOnlyCollection<FaturaResumoResponse> Items,
        int Page,
        int PageSize,
        int TotalItems,
        int TotalPages,
        FaturaListSummaryResponse Summary);

    private sealed record FaturaResumoResponse(
        Guid Id,
        Guid CartaoId,
        string CartaoNome,
        string Competencia,
        DateOnly DataFechamento,
        DateOnly DataVencimento,
        decimal ValorTotal,
        DateOnly? DataPagamento,
        string StatusCodigo,
        string StatusNome,
        int QuantidadeItens);

    private sealed record FaturaDetalheResponse(
        Guid Id,
        Guid CartaoId,
        string CartaoNome,
        string Competencia,
        DateOnly DataFechamento,
        DateOnly DataVencimento,
        decimal ValorTotal,
        DateOnly? DataPagamento,
        Guid? ContaBancariaPagamentoId,
        string? ContaBancariaPagamentoNome,
        string StatusCodigo,
        string StatusNome,
        string? Observacao,
        IReadOnlyCollection<FaturaItemResponse> Itens);

    private sealed record FaturaItemResponse(
        Guid ContaPagarId,
        string Descricao,
        string RecebedorNome,
        DateOnly DataCompra,
        decimal ValorLiquido,
        string StatusCodigo,
        int NumeroParcela,
        int QuantidadeParcelas);

    private sealed record MovimentacaoResumoResponse(
        Guid Id,
        DateOnly DataMovimentacao,
        string Tipo,
        string Natureza,
        string StatusCodigo,
        decimal Valor,
        Guid? ContaBancariaId,
        Guid? ContaPagarId,
        Guid? ContaReceberId,
        Guid? FaturaCartaoId,
        string? Observacao);

    private sealed record PagedResponse<T>(
        IReadOnlyCollection<T> Items,
        int Page,
        int PageSize,
        int TotalItems,
        int TotalPages);

    private sealed record ConfirmarImportacaoFaturaResponse(
        int ContasCriadas,
        int ContasDuplicadas);
}
