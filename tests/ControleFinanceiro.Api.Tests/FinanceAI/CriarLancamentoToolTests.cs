using System.Text.Json;
using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.FinanceAI.Tools;
using ControleFinanceiro.Domain.Identidade;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Api.Tests.FinanceAI;

public sealed class CriarLancamentoToolTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static ToolContext Contexto(PapelFamilia papel = PapelFamilia.Administrador) =>
        new(Guid.NewGuid(), Guid.NewGuid(), papel, "Família Teste");

    private async Task<(IServiceScope scope, CriarLancamentoTool tool, FinancialFixtureSeed.FixtureIds fixture)> PrepararAsync()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        return (scope, new CriarLancamentoTool(db), fixture);
    }

    [Fact]
    public async Task ExecuteAsync_ComInputValido_DeveCriarContaPagar()
    {
        var (scope, tool, fixture) = await PrepararAsync();
        using (scope)
        {
            var input = JsonSerializer.Serialize(new
            {
                descricao = "Mercado via IA",
                valor = 89.90,
                contaGerencialId = fixture.ContaGerencialDespesaId.ToString(),
                recebedorNome = "Supermercado IA"
            });

            var resultado = await tool.ExecuteAsync(input, Contexto(), CancellationToken.None);

            using var doc = JsonDocument.Parse(resultado);
            doc.RootElement.GetProperty("sucesso").GetBoolean().Should().BeTrue();

            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var conta = await db.ContasPagar.FirstOrDefaultAsync(c => c.Descricao == "Mercado via IA");
            conta.Should().NotBeNull();
            conta!.ValorLiquido.Should().Be(89.90m);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ComoVisualizador_DeveRetornarErro()
    {
        var (scope, tool, fixture) = await PrepararAsync();
        using (scope)
        {
            var input = JsonSerializer.Serialize(new
            {
                descricao = "Tentativa",
                valor = 10.0,
                contaGerencialId = fixture.ContaGerencialDespesaId.ToString()
            });

            var resultado = await tool.ExecuteAsync(input, Contexto(PapelFamilia.Visualizador), CancellationToken.None);

            JsonDocument.Parse(resultado).RootElement.TryGetProperty("erro", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task ExecuteAsync_ComValorInvalido_DeveRetornarErro()
    {
        var (scope, tool, fixture) = await PrepararAsync();
        using (scope)
        {
            var input = JsonSerializer.Serialize(new
            {
                descricao = "Sem valor",
                valor = 0.0,
                contaGerencialId = fixture.ContaGerencialDespesaId.ToString()
            });

            var resultado = await tool.ExecuteAsync(input, Contexto(), CancellationToken.None);

            JsonDocument.Parse(resultado).RootElement.TryGetProperty("erro", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task ExecuteAsync_ComContaGerencialInvalida_DeveRetornarErro()
    {
        var (scope, tool, _) = await PrepararAsync();
        using (scope)
        {
            var input = JsonSerializer.Serialize(new
            {
                descricao = "Categoria errada",
                valor = 50.0,
                contaGerencialId = "nao-e-guid"
            });

            var resultado = await tool.ExecuteAsync(input, Contexto(), CancellationToken.None);

            JsonDocument.Parse(resultado).RootElement.TryGetProperty("erro", out _).Should().BeTrue();
        }
    }
}
