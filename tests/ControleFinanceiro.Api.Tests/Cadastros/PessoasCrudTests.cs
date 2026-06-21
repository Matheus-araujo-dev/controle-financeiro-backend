using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Cadastros;

public sealed class PessoasCrudTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private sealed record Pessoa(Guid Id, string Nome, bool Ativo);

    [Fact]
    public async Task CriarComChavePix_ObterEAtualizar()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var criar = await client.PostAsJsonAsync("/api/v1/pessoas", new
        {
            nome = "Fornecedor PIX",
            tipoPessoa = "Juridica",
            cpfCnpj = "12.345.678/0001-90",
            email = "fornecedor@teste.local",
            telefone = "11999998888",
            observacao = "teste",
            chavesPix = new[] { new { tipo = "Email", chave = "fornecedor@teste.local" } }
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        var criada = await criar.Content.ReadFromJsonAsync<Pessoa>();
        criada!.Nome.Should().Be("Fornecedor PIX");

        var detalhe = await client.GetFromJsonAsync<Pessoa>($"/api/v1/pessoas/{criada.Id}");
        detalhe!.Id.Should().Be(criada.Id);

        var atualizar = await client.PutAsJsonAsync($"/api/v1/pessoas/{criada.Id}", new
        {
            nome = "Fornecedor PIX (alterado)",
            tipoPessoa = "Juridica",
            cpfCnpj = "12.345.678/0001-90",
            email = "novo@teste.local",
            telefone = (string?)null,
            observacao = (string?)null,
            chavesPix = new[] { new { tipo = "Aleatoria", chave = "a1b2c3d4-e5f6" } }
        });
        atualizar.StatusCode.Should().Be(HttpStatusCode.OK);
        var atualizada = await atualizar.Content.ReadFromJsonAsync<Pessoa>();
        atualizada!.Nome.Should().Be("Fornecedor PIX (alterado)");
    }

    [Fact]
    public async Task ObterPorId_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.GetAsync($"/api/v1/pessoas/{Guid.NewGuid()}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
