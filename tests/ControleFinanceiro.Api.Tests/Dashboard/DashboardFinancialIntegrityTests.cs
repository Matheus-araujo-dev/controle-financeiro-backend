using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Dashboard;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Contracts.Financeiro.Faturas;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Api.Tests.Dashboard;

public sealed class DashboardFinancialIntegrityTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Theory]
    [InlineData(true, 10, 20, "2027-01", "2027-01-20")]
    [InlineData(false, 10, 20, "2027-01", "2027-01-20")]
    [InlineData(true, 20, 10, "2027-01", "2027-02-10")]
    public async Task Resumo_DeveContarCompraUmaVezDuranteTodoCicloDaFatura(
        bool consolidar, int fechamento, int vencimento, string competencia, string dataVencimento)
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        using (var scope = factory.Services.CreateWorkspaceScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var cartao = await db.Cartoes.SingleAsync(c => c.Id == fixture.CartaoId);
            cartao.Atualizar(cartao.Nome, cartao.Bandeira, cartao.NumeroFinal, fechamento, vencimento,
                fixture.ContaBancariaId, 5000m, true,
                recebedorPadraoFaturaId: consolidar ? fixture.RecebedorId : null,
                formaPagamentoPadraoFaturaId: consolidar ? fixture.FormaPagamentoManualId : null);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        var compra = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2027-01-05", dataVencimento,
            responsavelCompraId = fixture.ResponsavelId, recebedorId = fixture.RecebedorId,
            formaPagamentoId = fixture.FormaPagamentoCartaoId, cartaoId = fixture.CartaoId,
            valorOriginal = 300m, quantidadeParcelas = 1, descricao = "Compra única",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 300m } }
        });
        compra.EnsureSuccessStatusCode();
        var faturas = await client.GetFromJsonAsync<FaturaListResponse>("/api/v1/faturas");
        var faturaId = faturas!.Items.Single(f => f.Competencia == competencia).Id;
        var mesResumo = dataVencimento[..7];

        await AssertTotalAsync(client, mesResumo, 300m);
        (await client.PostAsync($"/api/v1/faturas/{faturaId}/fechar", null)).EnsureSuccessStatusCode();
        await AssertTotalAsync(client, mesResumo, 300m);
        (await client.PostAsJsonAsync($"/api/v1/faturas/{faturaId}/pagar", new
        {
            dataPagamento = dataVencimento, contaBancariaPagamentoId = fixture.ContaBancariaId
        })).EnsureSuccessStatusCode();
        await AssertTotalAsync(client, mesResumo, 0m);
        (await client.PostAsync($"/api/v1/faturas/{faturaId}/estornar", null)).EnsureSuccessStatusCode();
        await AssertTotalAsync(client, mesResumo, 300m);
        // Estorno reabre o pagamento; reabertura de fechamento cancela a obrigação preservando o histórico.
        (await client.PostAsync($"/api/v1/faturas/{faturaId}/fechar", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/v1/faturas/{faturaId}/reabrir", null)).EnsureSuccessStatusCode();
        await AssertTotalAsync(client, mesResumo, 300m);
        if (consolidar)
        {
            using var scope = factory.Services.CreateWorkspaceScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var anterior = await db.ContasPagar.SingleAsync(c => c.FaturaCartaoId == faturaId && c.CartaoId == null);
            anterior.StatusContaId.Should().Be(StatusConta.CanceladaId);
            (await db.MovimentacoesFinanceiras.CountAsync(m => m.ContaPagarId == anterior.Id)).Should().Be(1,
                "a reabertura deve preservar a movimentação estornada e sua referência histórica");
        }
        (await client.PostAsync($"/api/v1/faturas/{faturaId}/fechar", null)).EnsureSuccessStatusCode();
        await AssertTotalAsync(client, mesResumo, 300m);
        if (consolidar)
        {
            using var scope = factory.Services.CreateWorkspaceScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var contas = await db.ContasPagar.Where(c => c.FaturaCartaoId == faturaId && c.CartaoId == null).ToListAsync();
            contas.Should().HaveCount(2);
            contas.Count(c => c.StatusContaId != StatusConta.CanceladaId).Should().Be(1);
        }
        (await client.PostAsJsonAsync($"/api/v1/faturas/{faturaId}/pagar", new
        {
            dataPagamento = dataVencimento, contaBancariaPagamentoId = fixture.ContaBancariaId
        })).EnsureSuccessStatusCode();
        await AssertTotalAsync(client, mesResumo, 0m);
        (await client.PostAsync($"/api/v1/faturas/{faturaId}/estornar", null)).EnsureSuccessStatusCode();
        await AssertTotalAsync(client, mesResumo, 300m);
    }

    private static async Task AssertTotalAsync(HttpClient client, string mes, decimal esperado)
    {
        var resumo = await client.GetFromJsonAsync<DashboardResumoResponse>($"/api/v1/dashboard/resumo?mesReferencia={mes}");
        resumo!.TotalAPagar.Should().Be(esperado, "uma compra deve aparecer uma única vez, representada pelos itens ou pela obrigação consolidada");
    }
}
