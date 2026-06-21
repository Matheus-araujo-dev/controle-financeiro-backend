using System.Text.Json;
using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.FinanceAI.Tools;
using ControleFinanceiro.Domain.Identidade;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Api.Tests.FinanceAI;

public sealed class FinanceToolsQueryTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [SqlServerFact]
    public async Task FerramentasDeConsulta_DeveExecutarERetornarJsonValido()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        using var scope = _factory.Services.CreateScope();
        var tools = scope.ServiceProvider.GetServices<IFinanceTool>().ToDictionary(t => t.Name);
        var contexto = new ToolContext(Guid.NewGuid(), Guid.NewGuid(), PapelFamilia.Administrador, "Família Teste");

        var casos = new Dictionary<string, string>
        {
            ["listar_categorias"] = "{}",
            ["buscar_saldo_atual"] = "{}",
            ["listar_pessoas"] = "{}",
            ["listar_meios_pagamento"] = "{}",
            ["buscar_resumo_mensal"] = """{"mes":4,"ano":2026}""",
            ["buscar_gastos_por_categoria"] = """{"mes":4,"ano":2026}""",
            ["buscar_gastos_por_responsavel"] = """{"mes":4,"ano":2026}"""
        };

        foreach (var (nome, input) in casos)
        {
            tools.Should().ContainKey(nome);

            var resultado = await tools[nome].ExecuteAsync(input, contexto, CancellationToken.None);

            resultado.Should().NotBeNullOrWhiteSpace($"a ferramenta {nome} deve retornar conteúdo");
            var parse = () => JsonDocument.Parse(resultado);
            parse.Should().NotThrow($"a ferramenta {nome} deve retornar JSON válido");
        }
    }

    [Fact]
    public async Task ListarPessoas_ComFiltroDeNome_DeveExecutar()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        using var scope = _factory.Services.CreateScope();
        var tool = scope.ServiceProvider.GetServices<IFinanceTool>().Single(t => t.Name == "listar_pessoas");
        var contexto = new ToolContext(Guid.NewGuid(), Guid.NewGuid(), PapelFamilia.Membro, "Família Teste");

        var resultado = await tool.ExecuteAsync("""{"nome":"Cliente"}""", contexto, CancellationToken.None);

        var parse = () => JsonDocument.Parse(resultado);
        parse.Should().NotThrow();
    }
}
