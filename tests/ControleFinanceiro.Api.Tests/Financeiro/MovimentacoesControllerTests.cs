using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class MovimentacoesControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Get_ComFiltroDeTipoDeveRetornarSummaryFiltrado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var contaPagarResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 81m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Saída operacional",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 81m }
            }
        });

        var contaReceberResponse = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-04",
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 200m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Entrada operacional",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 200m }
            }
        });

        var contaPagar = await contaPagarResponse.Content.ReadFromJsonAsync<ContaIdResponse>();
        var contaReceber = await contaReceberResponse.Content.ReadFromJsonAsync<ContaIdResponse>();

        var liquidarSaidaResponse = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{contaPagar!.Id}/liquidar", new
        {
            dataLiquidacao = "2026-04-07",
            contaBancariaId = fixture.ContaBancariaId
        });

        var liquidarEntradaResponse = await client.PostAsJsonAsync($"/api/v1/contas-receber/{contaReceber!.Id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId
        });

        var listResponse = await client.GetFromJsonAsync<MovimentacaoListResponse>("/api/v1/movimentacoes?tipo=Saida");

        liquidarSaidaResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        liquidarEntradaResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().ContainSingle(item => item.Tipo == "Saida" && item.Valor == 81m);
        listResponse.Summary.TotalRegistros.Should().Be(1);
        listResponse.Summary.TotalEntradas.Should().Be(0m);
        listResponse.Summary.TotalSaidas.Should().Be(81m);
        listResponse.Summary.SaldoLiquido.Should().Be(-81m);
    }

    [Fact]
    public async Task Get_NaoDeveListarMovimentacoesCanceladas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 120m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta cancelada na movimentacao",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 120m }
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<ContaIdResponse>();

        var liquidarResponse = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{created!.Id}/liquidar", new
        {
            dataLiquidacao = "2026-04-06",
            contaBancariaId = fixture.ContaBancariaId
        });

        var estornarResponse = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{created.Id}/estornar", new { });
        var listResponse = await client.GetFromJsonAsync<MovimentacaoListResponse>("/api/v1/movimentacoes?search=Conta cancelada na movimentacao");

        liquidarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        estornarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().BeEmpty();
        listResponse.Summary.TotalRegistros.Should().Be(0);
        listResponse.Summary.TotalEntradas.Should().Be(0m);
        listResponse.Summary.TotalSaidas.Should().Be(0m);
        listResponse.Summary.SaldoLiquido.Should().Be(0m);
    }

    [Fact]
    public async Task Get_ComFiltroDeMultiplasContasBancariasDeveRetornarMovimentosDasContasSelecionadas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var contaBancariaSecundariaId = await CreateContaBancariaAsync(client, "Conta secundaria");

        var primeiraContaResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 90m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Saida conta principal",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 90m }
            }
        });

        var segundaContaResponse = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-04",
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 140m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Entrada conta secundaria",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 140m }
            }
        });

        var primeiraConta = await primeiraContaResponse.Content.ReadFromJsonAsync<ContaIdResponse>();
        var segundaConta = await segundaContaResponse.Content.ReadFromJsonAsync<ContaIdResponse>();

        var liquidarPrimeiraResponse = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{primeiraConta!.Id}/liquidar", new
        {
            dataLiquidacao = "2026-04-07",
            contaBancariaId = fixture.ContaBancariaId
        });

        var liquidarSegundaResponse = await client.PostAsJsonAsync($"/api/v1/contas-receber/{segundaConta!.Id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = contaBancariaSecundariaId
        });

        var listResponse = await client.GetFromJsonAsync<MovimentacaoListResponse>(
            $"/api/v1/movimentacoes?contaBancariaIds={fixture.ContaBancariaId},{contaBancariaSecundariaId}");

        liquidarPrimeiraResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        liquidarSegundaResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().HaveCount(2);
        listResponse.Items.Select(item => item.ContaBancariaId)
            .Should()
            .BeEquivalentTo(new Guid?[] { fixture.ContaBancariaId, contaBancariaSecundariaId });
        listResponse.Summary.TotalRegistros.Should().Be(2);
    }

    [Fact]
    public async Task Get_ComFiltroDeMultiplosResponsaveisDeveRetornarMovimentosDosResponsaveisSelecionados()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var responsavelSecundarioId = await CriarPessoaAsync(client, "Responsavel secundario");

        var contaPagarResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            responsavelCompraId = fixture.ResponsavelId,
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 90m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Saida com responsavel principal",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 90m }
            }
        });

        var contaReceberResponse = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-04",
            responsavelId = responsavelSecundarioId,
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 140m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Entrada com responsavel secundario",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 140m }
            }
        });

        var contaPagar = await contaPagarResponse.Content.ReadFromJsonAsync<ContaIdResponse>();
        var contaReceber = await contaReceberResponse.Content.ReadFromJsonAsync<ContaIdResponse>();

        var liquidarSaidaResponse = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{contaPagar!.Id}/liquidar", new
        {
            dataLiquidacao = "2026-04-07",
            contaBancariaId = fixture.ContaBancariaId
        });

        var liquidarEntradaResponse = await client.PostAsJsonAsync($"/api/v1/contas-receber/{contaReceber!.Id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId
        });

        var listResponse = await client.GetFromJsonAsync<MovimentacaoListResponse>(
            $"/api/v1/movimentacoes?responsavelIds={fixture.ResponsavelId},{responsavelSecundarioId}");

        liquidarSaidaResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        liquidarEntradaResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().HaveCount(2);
        listResponse.Summary.TotalRegistros.Should().Be(2);
    }

    private static async Task<Guid> CreateContaBancariaAsync(HttpClient client, string nome)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-bancarias", new
        {
            nome,
            banco = "Banco Exemplo",
            agencia = "0001",
            numeroConta = "12345-7",
            tipoConta = "Corrente",
            saldoInicial = 1000m,
            dataSaldoInicial = "2026-04-01",
            ativo = true
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ContaIdResponse>();
        return payload!.Id;
    }

    private static async Task<Guid> CriarPessoaAsync(HttpClient client, string nome)
    {
        var response = await client.PostAsJsonAsync("/api/v1/pessoas", new
        {
            nome,
            tipoPessoa = "Fisica"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ContaIdResponse>();
        return payload!.Id;
    }

    private sealed record ContaIdResponse(Guid Id);

    private sealed record MovimentacaoListSummaryResponse(
        int TotalRegistros,
        decimal TotalEntradas,
        decimal TotalSaidas,
        decimal SaldoLiquido);

    private sealed record MovimentacaoListResponse(
        IReadOnlyCollection<MovimentacaoResumoResponse> Items,
        int Page,
        int PageSize,
        int TotalItems,
        int TotalPages,
        MovimentacaoListSummaryResponse Summary);

    private sealed record MovimentacaoResumoResponse(
        Guid Id,
        DateOnly DataMovimentacao,
        string Tipo,
        string Natureza,
        string StatusCodigo,
        string StatusNome,
        decimal Valor,
        Guid? ContaBancariaId,
        string? ContaBancariaNome,
        Guid? ContaPagarId,
        Guid? ContaReceberId,
        Guid? FaturaCartaoId,
        string? Observacao);
}
