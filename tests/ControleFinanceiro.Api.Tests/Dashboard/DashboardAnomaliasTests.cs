using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Dashboard;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Api.Tests.Dashboard;

public sealed class DashboardAnomaliasTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task ExigeAutenticacao()
    {
        using var client = factory.CreateAnonymousClient();
        (await client.GetAsync("/api/v1/dashboard/anomalias?mesReferencia=2027-01"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LimiteNaoProduzDiagnosticoParcial()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        using var scope = factory.Services.CreateWorkspaceScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var dia = new DateOnly(2027, 1, 5);
        for (var i = 0; i < 5001; i++)
            db.ContasPagar.Add(ContaPagar.Criar(null, dia, fixture.ResponsavelId, fixture.RecebedorId,
                dia, fixture.FormaPagamentoManualId, null, null, 100, 0, 0, 0, 1, 1, null, null,
                "Internet", null, StatusConta.PendenteId, false, null, OrigemLancamento.Manual,
                [new RateioPlano(fixture.ContaGerencialDespesaId, 100)]));
        await db.SaveChangesAsync(CancellationToken.None);
        var result = await scope.ServiceProvider.GetRequiredService<DashboardAnomaliasService>()
            .ObterAsync("2027-01", CancellationToken.None);
        result.Completo.Should().BeFalse();
        result.ContasAnalisadas.Should().Be(5001);
        result.Itens.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectaDuplicidadeComEvidenciasSemAlterarContas()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        for (var i = 0; i < 2; i++)
        {
            var result = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
            {
                dataEmissao = "2027-01-05", dataVencimento = "2027-01-10",
                responsavelCompraId = fixture.ResponsavelId, recebedorId = fixture.RecebedorId,
                formaPagamentoId = fixture.FormaPagamentoManualId, valorOriginal = 100m,
                quantidadeParcelas = 1, descricao = "Internet",
                rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 100m } }
            });
            result.EnsureSuccessStatusCode();
        }
        var response = await client.GetAsync("/api/v1/dashboard/anomalias?mesReferencia=2027-01");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        json["completo"]!.GetValue<bool>().Should().BeTrue();
        var alert = json["itens"]!.AsArray().Single()!;
        alert["tipo"]!.GetValue<string>().Should().Be("DuplicidadeProvavel");
        alert["evidencias"]!.AsArray().Should().HaveCount(2);
        var again = await client.GetStringAsync("/api/v1/dashboard/anomalias?mesReferencia=2027-01");
        JsonNode.DeepEquals(json, JsonNode.Parse(again)).Should().BeTrue();
        using var scope = factory.Services.CreateWorkspaceScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        (await db.ContasPagar.CountAsync()).Should().Be(2);
        db.DefinirWorkspaceCorrente(Guid.NewGuid());
        var other = await scope.ServiceProvider.GetRequiredService<DashboardAnomaliasService>()
            .ObterAsync("2027-01", CancellationToken.None);
        other.ContasAnalisadas.Should().Be(0);
        other.Itens.Should().BeEmpty();
    }

    [Fact]
    public async Task ExcluiCancelamentosParcelasECreditos()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        using var scope = factory.Services.CreateWorkspaceScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var dia = new DateOnly(2027, 1, 5);
        foreach (var (valor, parcelas, status) in new[] {
                     (100m, 1, StatusConta.PendenteId), (100m, 1, StatusConta.CanceladaId),
                     (100m, 2, StatusConta.PendenteId), (-100m, 1, StatusConta.PendenteId) })
        {
            db.ContasPagar.Add(ContaPagar.Criar(null, dia, fixture.ResponsavelId, fixture.RecebedorId,
                dia, valor < 0 ? fixture.FormaPagamentoCartaoId : fixture.FormaPagamentoManualId,
                valor < 0 ? fixture.CartaoId : null, null, valor, 0, 0, 0, parcelas, 1,
                parcelas > 1 ? Guid.NewGuid() : null, null, "Internet", null, status, false, null,
                OrigemLancamento.Manual, [new RateioPlano(fixture.ContaGerencialDespesaId, valor)]));
        }
        await db.SaveChangesAsync(CancellationToken.None);
        var result = await scope.ServiceProvider.GetRequiredService<DashboardAnomaliasService>()
            .ObterAsync("2027-01", CancellationToken.None);
        result.ContasAnalisadas.Should().Be(1);
        result.Itens.Should().BeEmpty();
    }

    [Theory]
    [InlineData("errado")]
    [InlineData("0001-01")]
    [InlineData("9999-12")]
    public async Task RejeitaMesInvalido(string mes)
    {
        using var client = factory.CreateClient();
        (await client.GetAsync($"/api/v1/dashboard/anomalias?mesReferencia={mes}"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
