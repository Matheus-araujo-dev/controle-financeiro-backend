using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Contracts.Financeiro.Orcamentos;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class OrcamentosFluxoTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task UpsertObterERemoverMeta()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        // Competência vazia funciona
        var vazio = await client.GetAsync("/api/v1/orcamentos?competencia=2026-04");
        vazio.StatusCode.Should().Be(HttpStatusCode.OK);

        // Upsert de meta
        var upsert = await client.PutAsJsonAsync("/api/v1/orcamentos/metas",
            new UpsertMetaOrcamentoRequest(fixture.ContaGerencialDespesaId, "2026-04", 1500m));
        upsert.StatusCode.Should().Be(HttpStatusCode.OK);
        var meta = await upsert.Content.ReadFromJsonAsync<MetaOrcamentoResponse>();
        meta!.ValorMeta.Should().Be(1500m);

        // Upsert de novo (atualiza a mesma meta)
        var upsert2 = await client.PutAsJsonAsync("/api/v1/orcamentos/metas",
            new UpsertMetaOrcamentoRequest(fixture.ContaGerencialDespesaId, "2026-04", 2000m));
        var meta2 = await upsert2.Content.ReadFromJsonAsync<MetaOrcamentoResponse>();
        meta2!.ValorMeta.Should().Be(2000m);
        meta2.Id.Should().Be(meta.Id);

        // GET reflete a meta
        var comMeta = await client.GetFromJsonAsync<OrcamentoCompetenciaResponse>("/api/v1/orcamentos?competencia=2026-04");
        comMeta!.Itens.Should().Contain(i => i.ContaGerencialId == fixture.ContaGerencialDespesaId);

        // Remover
        var remover = await client.DeleteAsync($"/api/v1/orcamentos/metas/{meta.Id}");
        remover.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoverMeta_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.DeleteAsync($"/api/v1/orcamentos/metas/{Guid.NewGuid()}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
