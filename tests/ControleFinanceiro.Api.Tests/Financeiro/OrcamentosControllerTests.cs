using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class OrcamentosControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task GetOrcamento_DeveConsolidarMetasRealizadoPercentualEEstouro()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        await UpsertMetaAsync(client, fixture.ContaGerencialDespesaId, "2026-04", 200m);
        await UpsertMetaAsync(client, fixture.ContaGerencialAdministrativaId, "2026-04", 100m);

        await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-04-05",
            dataVencimento: "2026-04-10",
            valor: 150m,
            descricao: "Servico operacional",
            contaGerencialId: fixture.ContaGerencialDespesaId);

        await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-04-06",
            dataVencimento: "2026-04-12",
            valor: 130m,
            descricao: "Licenca administrativa",
            contaGerencialId: fixture.ContaGerencialAdministrativaId);

        await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-05-01",
            dataVencimento: "2026-05-05",
            valor: 999m,
            descricao: "Fora da competencia",
            contaGerencialId: fixture.ContaGerencialDespesaId);

        var orcamento = await client.GetFromJsonAsync<OrcamentoCompetenciaResponse>(
            "/api/v1/orcamentos?competencia=2026-04");

        orcamento.Should().NotBeNull();
        orcamento!.Competencia.Should().Be("2026-04");
        orcamento.TotalMeta.Should().Be(300m);
        orcamento.TotalRealizado.Should().Be(280m);
        orcamento.PercentualConsumido.Should().Be(93.33m);
        orcamento.PossuiEstouro.Should().BeTrue();

        orcamento.Itens.Should().Contain(item =>
            item.ContaGerencialId == fixture.ContaGerencialDespesaId &&
            item.ValorMeta == 200m &&
            item.ValorRealizado == 150m &&
            item.PercentualConsumido == 75m &&
            !item.Estourado);

        orcamento.Itens.Should().Contain(item =>
            item.ContaGerencialId == fixture.ContaGerencialAdministrativaId &&
            item.ValorMeta == 100m &&
            item.ValorRealizado == 130m &&
            item.PercentualConsumido == 130m &&
            item.Estourado);

        orcamento.Itens.Should().NotContain(item => item.ContaGerencialId == fixture.ContaGerencialReceitaId);
    }


    [Fact]
    public async Task GetOrcamento_DeveConsolidarContaPaiPelaSomaDasFilhasEOrdenarPorCodigo()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var contaPaiId = await CriarContaGerencialAsync(client, "DESP.10", "Despesas estruturais", null);
        var contaFilhaBId = await CriarContaGerencialAsync(client, "DESP.10.02", "Filha B", contaPaiId);
        var contaFilhaAId = await CriarContaGerencialAsync(client, "DESP.10.01", "Filha A", contaPaiId);

        await UpsertMetaAsync(client, contaFilhaAId, "2026-04", 100m);
        await UpsertMetaAsync(client, contaFilhaBId, "2026-04", 250m);

        await CriarContaPagarAsync(client, fixture, "2026-04-05", "2026-04-10", 40m, "Despesa filha A", contaFilhaAId);
        await CriarContaPagarAsync(client, fixture, "2026-04-06", "2026-04-12", 70m, "Despesa filha B", contaFilhaBId);

        var orcamento = await client.GetFromJsonAsync<OrcamentoCompetenciaResponse>("/api/v1/orcamentos?competencia=2026-04");

        orcamento.Should().NotBeNull();
        orcamento!.TotalMeta.Should().Be(350m);
        orcamento.TotalRealizado.Should().Be(110m);
        orcamento.Itens.Select(item => item.ContaGerencialCodigo).Should().BeInAscendingOrder();
        orcamento.Itens.Should().Contain(item =>
            item.ContaGerencialId == contaPaiId &&
            item.ValorMeta == 350m &&
            item.ValorRealizado == 110m &&
            item.AceitaLancamentos == false);
        orcamento.Itens.Should().Contain(item =>
            item.ContaGerencialId == contaFilhaAId &&
            item.ContaPaiId == contaPaiId &&
            item.AceitaLancamentos);
    }

    [Fact]
    public async Task PutMeta_QuandoContaGerencialForPai_DeveRetornarBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var contaPaiId = await CriarContaGerencialAsync(client, "DESP.20", "Conta pai", null);
        _ = await CriarContaGerencialAsync(client, "DESP.20.01", "Conta filha", contaPaiId);

        var response = await client.PutAsJsonAsync("/api/v1/orcamentos/metas", new
        {
            contaGerencialId = contaPaiId,
            competencia = "2026-04",
            valorMeta = 100m
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutMeta_QuandoMetaJaExiste_DeveAtualizarValorSemDuplicar()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var criada = await UpsertMetaAsync(client, fixture.ContaGerencialDespesaId, "2026-04", 200m);
        var atualizada = await UpsertMetaAsync(client, fixture.ContaGerencialDespesaId, "2026-04", 350m);

        atualizada.Id.Should().Be(criada.Id);
        atualizada.ValorMeta.Should().Be(350m);

        var orcamento = await client.GetFromJsonAsync<OrcamentoCompetenciaResponse>(
            "/api/v1/orcamentos?competencia=2026-04");

        orcamento.Should().NotBeNull();
        orcamento!.TotalMeta.Should().Be(350m);
        orcamento.Itens.Should().ContainSingle(item => item.MetaId == criada.Id);
    }

    [Fact]
    public async Task PutMeta_QuandoContaGerencialForReceita_DeveRetornarBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var response = await client.PutAsJsonAsync("/api/v1/orcamentos/metas", new
        {
            contaGerencialId = fixture.ContaGerencialReceitaId,
            competencia = "2026-04",
            valorMeta = 100m
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutMeta_QuandoValorMetaForInvalido_DeveRetornarBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var response = await client.PutAsJsonAsync("/api/v1/orcamentos/metas", new
        {
            contaGerencialId = fixture.ContaGerencialDespesaId,
            competencia = "2026-04",
            valorMeta = 0m
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetOrcamento_QuandoCompetenciaForInvalida_DeveRetornarBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var semCompetencia = await client.GetAsync("/api/v1/orcamentos");
        var competenciaInvalida = await client.GetAsync("/api/v1/orcamentos?competencia=abc");

        semCompetencia.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        competenciaInvalida.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteMeta_DeveRemoverMetaERetornarNotFoundQuandoInexistente()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var meta = await UpsertMetaAsync(client, fixture.ContaGerencialDespesaId, "2026-04", 200m);

        var removerResponse = await client.DeleteAsync($"/api/v1/orcamentos/metas/{meta.Id}");
        removerResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var orcamento = await client.GetFromJsonAsync<OrcamentoCompetenciaResponse>(
            "/api/v1/orcamentos?competencia=2026-04");

        orcamento.Should().NotBeNull();
        orcamento!.Itens.Should().NotContain(item => item.MetaId == meta.Id);

        var removerNovamenteResponse = await client.DeleteAsync($"/api/v1/orcamentos/metas/{meta.Id}");
        removerNovamenteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<Guid> CriarContaGerencialAsync(HttpClient client, string codigo, string descricao, Guid? contaPaiId)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo,
            descricao,
            tipo = "Despesa",
            contaPaiId,
            ativo = true
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private static async Task<MetaOrcamentoResponse> UpsertMetaAsync(
        HttpClient client,
        Guid contaGerencialId,
        string competencia,
        decimal valorMeta)
    {
        var response = await client.PutAsJsonAsync("/api/v1/orcamentos/metas", new
        {
            contaGerencialId,
            competencia,
            valorMeta
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<MetaOrcamentoResponse>();
        return payload!;
    }

    private static async Task<Guid> CriarContaPagarAsync(
        HttpClient client,
        FinancialFixtureSeed.FixtureIds fixture,
        string dataEmissao,
        string dataVencimento,
        decimal valor,
        string descricao,
        Guid contaGerencialId)
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
                new { contaGerencialId, valor }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record MetaOrcamentoResponse(
        Guid Id,
        Guid ContaGerencialId,
        string Competencia,
        decimal ValorMeta);

    private sealed record OrcamentoCompetenciaResponse(
        string Competencia,
        decimal TotalMeta,
        decimal TotalRealizado,
        decimal? PercentualConsumido,
        bool PossuiEstouro,
        IReadOnlyCollection<OrcamentoItemResponse> Itens);

    private sealed record OrcamentoItemResponse(
        Guid? MetaId,
        Guid ContaGerencialId,
        Guid? ContaPaiId,
        string? ContaGerencialCodigo,
        string ContaGerencialDescricao,
        decimal? ValorMeta,
        decimal ValorRealizado,
        decimal? PercentualConsumido,
        bool Estourado,
        bool AceitaLancamentos);
}
