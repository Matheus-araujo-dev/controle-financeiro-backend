using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Contracts.Financeiro.Fechamentos;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class FechamentoMensalApiTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task FecharEReabrir_CompetenciaSemBloqueios_DevePersistirAuditoria()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var diagnostico = await client.GetFromJsonAsync<FechamentoMensalResponse>("/api/v1/fechamentos/2026-09");
        diagnostico!.Status.Should().Be("Aberto");
        diagnostico.ProntoParaFechar.Should().BeTrue();

        var fechamentoResponse = await client.PostAsync("/api/v1/fechamentos/2026-09/fechar", null);
        fechamentoResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fechamento = await fechamentoResponse.Content.ReadFromJsonAsync<FechamentoMensalResponse>();
        fechamento!.Status.Should().Be("Fechado");
        fechamento.FechadoPorUsuarioId.Should().NotBeNull();
        fechamento.FechadoEmUtc.Should().NotBeNull();

        var reaberturaResponse = await client.PostAsJsonAsync("/api/v1/fechamentos/2026-09/reabrir",
            new ReabrirFechamentoMensalRequest("Correção de lançamento"));
        reaberturaResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reabertura = await reaberturaResponse.Content.ReadFromJsonAsync<FechamentoMensalResponse>();
        reabertura!.Status.Should().Be("Reaberto");
        reabertura.JustificativaReabertura.Should().Be("Correção de lançamento");
    }
}
