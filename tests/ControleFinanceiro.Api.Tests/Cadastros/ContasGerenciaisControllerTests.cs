using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Cadastros;

public sealed class ContasGerenciaisControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task PostEPut_QuandoGerarCiclo_DeveRetornarErroDeValidacao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var parentResponse = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo = "DESP-ADM",
            descricao = "Administrativo",
            tipo = "Despesa",
            ativo = true
        });

        var parent = await parentResponse.Content.ReadFromJsonAsync<ContaGerencialResponse>();

        var childResponse = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo = "DESP-MKT",
            descricao = "Marketing",
            tipo = "Despesa",
            contaPaiId = parent!.Id,
            ativo = true
        });

        var child = await childResponse.Content.ReadFromJsonAsync<ContaGerencialResponse>();

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/contas-gerenciais/{parent.Id}", new
        {
            codigo = "DESP-ADM",
            descricao = "Administrativo",
            tipo = "Despesa",
            contaPaiId = child!.Id,
            ativo = true
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record ContaGerencialResponse(
        Guid Id,
        string? Codigo,
        string Descricao,
        string Tipo,
        Guid? ContaPaiId,
        string? ContaPaiDescricao,
        bool Ativo);
}
