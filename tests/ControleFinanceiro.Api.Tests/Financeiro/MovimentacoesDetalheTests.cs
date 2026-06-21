using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class MovimentacoesDetalheTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private sealed record Conta(Guid Id);
    private sealed record Mov(Guid Id, string Tipo, string Natureza, decimal Valor);
    private sealed record Paged(IReadOnlyCollection<Mov> Items, int TotalItems);

    [Fact]
    public async Task ListarEObterDetalhe_DeMovimentacaoGeradaPorLiquidacao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var criar = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 320m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Despesa movimentada",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 320m } }
        });
        var conta = await criar.Content.ReadFromJsonAsync<Conta>();

        await client.PostAsJsonAsync($"/api/v1/contas-pagar/{conta!.Id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId,
            valorLiquidacao = 320m,
            atualizarValorConta = true
        });

        var lista = await client.GetFromJsonAsync<Paged>("/api/v1/movimentacoes?search=Despesa movimentada");
        lista!.Items.Should().ContainSingle();
        var mov = lista.Items.Single();
        mov.Tipo.Should().Be("Saida");
        mov.Valor.Should().Be(320m);

        var detalhe = await client.GetFromJsonAsync<Mov>($"/api/v1/movimentacoes/{mov.Id}");
        detalhe!.Id.Should().Be(mov.Id);
    }

    [Fact]
    public async Task ObterPorId_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.GetAsync($"/api/v1/movimentacoes/{Guid.NewGuid()}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
