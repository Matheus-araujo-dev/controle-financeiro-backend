using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Importacoes;

public sealed class ImportacoesWhatsappQueryTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private sealed record Detalhe(Guid Id, string StatusCodigo);
    private sealed record Resumo(Guid Id, string StatusCodigo, string Remetente);
    private sealed record Paged(IReadOnlyCollection<Resumo> Items, int TotalItems);

    private static async Task<Guid> WebhookTextoAsync(HttpClient client, string texto, string remetente = "5511970001122")
    {
        var resp = await client.PostAsJsonAsync("/api/v1/importacoes-whatsapp/webhook", new
        {
            tipoOrigem = "Texto",
            remetente,
            textoBruto = texto
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<Detalhe>())!.Id;
    }

    [Fact]
    public async Task Listar_ComFiltrosDeBuscaEStatus_DeveFiltrar()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var id = await WebhookTextoAsync(client, "Pagar mercado livre 200,00 vencimento 2026-05-10");

        var porBusca = await client.GetFromJsonAsync<Paged>("/api/v1/importacoes-whatsapp?search=mercado");
        porBusca!.Items.Should().Contain(i => i.Id == id);

        var porStatus = await client.GetFromJsonAsync<Paged>("/api/v1/importacoes-whatsapp?statusCodigo=PENDENTE_REVISAO");
        porStatus!.Items.Should().Contain(i => i.Id == id);

        var semResultado = await client.GetFromJsonAsync<Paged>("/api/v1/importacoes-whatsapp?search=zzz-nao-existe");
        semResultado!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ObterPorId_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.GetAsync($"/api/v1/importacoes-whatsapp/{Guid.NewGuid()}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
