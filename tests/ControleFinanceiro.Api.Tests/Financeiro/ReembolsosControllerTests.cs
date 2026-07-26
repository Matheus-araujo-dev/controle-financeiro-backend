using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class ReembolsosControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task CriarReembolso_UmPagadorSemParcelar_DeveCriarUmaContaReceber()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        // Create conta a pagar first
        var pagarResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-01-10",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-02-10",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 500m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Despesa com reembolso",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 500m } }
        });

        pagarResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var pagar = await pagarResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        // Create reembolso
        var reembolsoResponse = await client.PostAsJsonAsync("/api/v1/reembolsos/contas-pagar", new
        {
            contaOrigemId = pagar!.Id,
            parcelarIgual = false,
            valorTotal = 500m,
            pagadoresIds = new[] { fixture.PagadorId },
            formaPagamentoId = fixture.FormaPagamentoManualId,
            dataVencimento = "2026-02-10",
            descricao = "Reembolso: Despesa com reembolso",
            observacao = (string?)null,
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 500m } }
        });

        reembolsoResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var reembolso = await reembolsoResponse.Content.ReadFromJsonAsync<ReembolsoResponse>();
        reembolso.Should().NotBeNull();
        reembolso!.ContasReceber.Should().HaveCount(1);
        reembolso.ContasReceber[0].ValorLiquido.Should().Be(500m);
    }

    [Fact]
    public async Task CriarReembolso_TresPagadoresComParcelas_DeveCriarDozeContasReceber()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        // Create three pagadores
        var pagador2 = await CreatePessoaAsync(client, "Pagador 2");
        var pagador3 = await CreatePessoaAsync(client, "Pagador 3");

        // Create conta a pagar with 4 parcelas
        var pagarResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-01-10",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-02-10",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 1000m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 4,
            descricao = "Servico parcelado",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 1000m } }
        });

        pagarResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var pagar = await pagarResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        // Create reembolso: R$750, 3 pagadores, parcelar igual (4 parcelas)
        var reembolsoResponse = await client.PostAsJsonAsync("/api/v1/reembolsos/contas-pagar", new
        {
            contaOrigemId = pagar!.Id,
            parcelarIgual = true,
            valorTotal = 750m,
            pagadoresIds = new[] { fixture.PagadorId, pagador2, pagador3 },
            formaPagamentoId = fixture.FormaPagamentoManualId,
            dataVencimento = "2026-02-10",
            descricao = "Reembolso: Servico parcelado",
            observacao = (string?)null,
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 750m } }
        });

        reembolsoResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var reembolso = await reembolsoResponse.Content.ReadFromJsonAsync<ReembolsoResponse>();
        reembolso.Should().NotBeNull();
        // 3 pagadores × 4 parcelas = 12 contas a receber
        reembolso!.ContasReceber.Should().HaveCount(12);
        reembolso.ContasReceber.Sum(c => c.ValorLiquido).Should().BeApproximately(750m, 0.10m);
    }

    [Fact]
    public async Task CriarReembolso_ContaJaTemReembolso_DeveRetornar400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var pagarResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-01-10",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-02-10",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 200m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta duplicada",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 200m } }
        });

        var pagar = await pagarResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        var reembolsoBody = new
        {
            contaOrigemId = pagar!.Id,
            parcelarIgual = false,
            valorTotal = 200m,
            pagadoresIds = new[] { fixture.PagadorId },
            formaPagamentoId = fixture.FormaPagamentoManualId,
            dataVencimento = "2026-02-10",
            descricao = "Reembolso",
            observacao = (string?)null,
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 200m } }
        };

        // First reembolso OK
        var r1 = await client.PostAsJsonAsync("/api/v1/reembolsos/contas-pagar", reembolsoBody);
        r1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Second should fail
        var r2 = await client.PostAsJsonAsync("/api/v1/reembolsos/contas-pagar", reembolsoBody);
        r2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CriarReembolso_ApoCriar_GetContaPagarDeveExibirGrupoReembolso()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var pagarResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-03-01",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-01",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 300m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta com grupo",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 300m } }
        });

        var pagar = await pagarResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        await client.PostAsJsonAsync("/api/v1/reembolsos/contas-pagar", new
        {
            contaOrigemId = pagar!.Id,
            parcelarIgual = false,
            valorTotal = 300m,
            pagadoresIds = new[] { fixture.PagadorId },
            formaPagamentoId = fixture.FormaPagamentoManualId,
            dataVencimento = "2026-04-01",
            descricao = "Reembolso",
            observacao = (string?)null,
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 300m } }
        });

        // GET the conta pagar detail — should expose GrupoReembolso
        var detalhe = await client.GetFromJsonAsync<ContaPagarDetalheResponse>($"/api/v1/contas-pagar/{pagar.Id}");
        detalhe.Should().NotBeNull();
        detalhe!.GrupoReembolsoId.Should().NotBeNull();
        detalhe.GrupoReembolso.Should().NotBeNull();
        detalhe.GrupoReembolso!.Contas.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task ContasPagar_ComDoisResponsaveis_DeveCriarDuasContas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var responsavel2 = await CreatePessoaAsync(client, "Responsavel 2");

        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-01",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-05-01",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 200m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Despesa multi responsavel",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 200m } },
            responsaveisAdicionaisIds = new[] { fixture.ResponsavelId, responsavel2 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var conta = await response.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        var detalhe = await client.GetFromJsonAsync<ContaPagarDetalheResponse>($"/api/v1/contas-pagar/{conta!.Id}");
        detalhe.Should().NotBeNull();
        detalhe!.GrupoResponsaveisId.Should().NotBeNull();
        detalhe.GrupoResponsaveis.Should().NotBeNull();
        detalhe.GrupoResponsaveis!.Contas.Should().HaveCount(2);

        var list = await client.GetFromJsonAsync<ContaListResponse>(
            "/api/v1/contas-pagar?search=multi+responsavel");
        list!.Items.Should().HaveCountGreaterOrEqualTo(2);
        list.Items.Sum(x => x.ValorLiquido).Should().BeApproximately(200m, 0.02m);
    }

    [Fact]
    public async Task ContasReceber_ComDoisPagadores_DeveCriarDuasContas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var pagador2 = await CreatePessoaAsync(client, "Pagador extra");

        var response = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            numeroDocumento = (string?)null,
            dataEmissao = "2026-04-01",
            responsavelId = (Guid?)null,
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-05-01",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            cartaoId = (Guid?)null,
            contaBancariaId = (Guid?)null,
            dataLiquidacao = (string?)null,
            valorOriginal = 300m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Receita multi pagador",
            observacao = (string?)null,
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 300m } },
            recorrencia = (object?)null,
            pagadoresAdicionaisIds = new[] { fixture.PagadorId, pagador2 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var conta = await response.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        var detalhe = await client.GetFromJsonAsync<ContaReceberDetalheResponse>($"/api/v1/contas-receber/{conta!.Id}");
        detalhe.Should().NotBeNull();
        detalhe!.GrupoResponsaveisId.Should().NotBeNull();
        detalhe.GrupoResponsaveis.Should().NotBeNull();
        detalhe.GrupoResponsaveis!.Contas.Should().HaveCount(2);

        var list = await client.GetFromJsonAsync<ContaListResponse>(
            "/api/v1/contas-receber?search=multi+pagador");
        list!.Items.Should().HaveCountGreaterOrEqualTo(2);
        list.Items.Sum(x => x.ValorLiquido).Should().BeApproximately(300m, 0.02m);
    }

    private static async Task<Guid> CreatePessoaAsync(HttpClient client, string nome)
    {
        var r = await client.PostAsJsonAsync("/api/v1/pessoas", new { nome, tipoPessoa = "Fisica" });
        r.EnsureSuccessStatusCode();
        var payload = await r.Content.ReadFromJsonAsync<IdPayload>();
        return payload!.Id;
    }

    private sealed record ContaDetalheResponse(Guid Id, decimal ValorLiquido, int NumeroParcela, int QuantidadeParcelas);

    private sealed record ContaPagarDetalheResponse(
        Guid Id, decimal ValorLiquido, Guid? GrupoReembolsoId, Guid? GrupoResponsaveisId,
        GrupoReembolsoInfo? GrupoReembolso, GrupoResponsaveisInfo? GrupoResponsaveis);

    private sealed record ContaReceberDetalheResponse(
        Guid Id, decimal ValorLiquido, Guid? GrupoReembolsoId, Guid? GrupoResponsaveisId,
        GrupoReembolsoInfo? GrupoReembolso, GrupoResponsaveisInfo? GrupoResponsaveis);

    private sealed record GrupoReembolsoInfo(Guid GrupoReembolsoId, ContaVinculadaItem[] Contas);
    private sealed record GrupoResponsaveisInfo(Guid GrupoResponsaveisId, ContaVinculadaItem[] Contas);
    private sealed record ContaVinculadaItem(Guid Id, decimal ValorLiquido);
    private sealed record ReembolsoResponse(Guid GrupoReembolsoId, ReembolsoContaItem[] ContasReceber);
    private sealed record ReembolsoContaItem(Guid Id, Guid PagadorId, int NumeroParcela, int QuantidadeParcelas, decimal ValorLiquido, string Descricao);
    private sealed record IdPayload(Guid Id);
    private sealed record ContaListResponse(ContaResumoItem[] Items);
    private sealed record ContaResumoItem(Guid Id, decimal ValorLiquido);
}
