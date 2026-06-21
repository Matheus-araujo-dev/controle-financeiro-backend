using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.PlanejamentoCompras;

public sealed class ComprasPlanejadasFluxoTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private sealed record Compra(Guid Id, string Titulo, string Status);

    private static async Task<Guid> CriarAsync(HttpClient client, FinancialFixtureSeed.FixtureIds fixture)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/compras-planejadas", new
        {
            titulo = "Notebook novo",
            descricao = "Troca de equipamento",
            valorEstimado = 4500m,
            dataDesejada = "2026-07-01",
            prioridade = "Alta",
            status = "Planejada",
            parcelavel = false,
            quantidadeParcelasDesejada = (int?)null,
            contaGerencialId = fixture.ContaGerencialDespesaId,
            responsavelId = fixture.ResponsavelId,
            link = (string?)null,
            observacao = (string?)null
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<Compra>())!.Id;
    }

    [Fact]
    public async Task CriarObterAtualizarERealizar()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarAsync(client, fixture);

        var detalhe = await client.GetFromJsonAsync<Compra>($"/api/v1/compras-planejadas/{id}");
        detalhe!.Titulo.Should().Be("Notebook novo");
        detalhe.Status.Should().Be("Planejada");

        var atualizar = await client.PutAsJsonAsync($"/api/v1/compras-planejadas/{id}", new
        {
            titulo = "Notebook novo (revisado)",
            descricao = "Troca de equipamento",
            valorEstimado = 5000m,
            dataDesejada = "2026-08-01",
            prioridade = "Media",
            status = "Planejada",
            parcelavel = true,
            quantidadeParcelasDesejada = 10,
            contaGerencialId = fixture.ContaGerencialDespesaId,
            responsavelId = fixture.ResponsavelId,
            link = (string?)null,
            observacao = "atualizada"
        });
        atualizar.StatusCode.Should().Be(HttpStatusCode.OK);

        var realizar = await client.PostAsJsonAsync($"/api/v1/compras-planejadas/{id}/realizar", new
        {
            dataCompra = "2026-07-15",
            dataVencimento = "2026-08-15",
            recebedorId = fixture.RecebedorId,
            formaPagamentoId = fixture.FormaPagamentoManualId,
            cartaoId = (Guid?)null,
            contaBancariaId = fixture.ContaBancariaId,
            quantidadeParcelas = 1,
            numeroDocumento = (string?)null,
            descricao = (string?)null,
            observacao = (string?)null
        });
        realizar.StatusCode.Should().Be(HttpStatusCode.OK);
        var realizada = await realizar.Content.ReadFromJsonAsync<Compra>();
        realizada!.Status.Should().Be("Comprada");

        // A compra virou uma conta a pagar
        var contas = await client.GetFromJsonAsync<PagedContas>("/api/v1/contas-pagar?search=Notebook");
        contas!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ObterPorId_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.GetAsync($"/api/v1/compras-planejadas/{Guid.NewGuid()}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record PagedContas(IReadOnlyCollection<object> Items);
}
