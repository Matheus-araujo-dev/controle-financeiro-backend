using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Cadastros;

public sealed class PessoasControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Post_DeveCriarPessoaEListarRegistro()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/v1/pessoas", new
        {
            nome = "Fornecedor Exemplo",
            tipoPessoa = "Juridica",
            cpfCnpj = "12.345.678/0001-90",
            email = "financeiro@example.com",
            telefone = "11999999999",
            observacao = "Cadastro inicial"
        });

        var created = await createResponse.Content.ReadFromJsonAsync<PessoaDetalheResponse>();
        var listResponse = await client.GetFromJsonAsync<PagedResponse<PessoaResumoResponse>>("/api/v1/pessoas?search=fornecedor");

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Should().NotBeNull();
        created!.CpfCnpj.Should().Be("12345678000190");
        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().ContainSingle(item => item.Nome == "Fornecedor Exemplo");
    }

    [Fact]
    public async Task PatchInativar_DeveMarcarPessoaComoInativa()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/v1/pessoas", new
        {
            nome = "Cliente Exemplo",
            tipoPessoa = "Fisica"
        });

        var created = await createResponse.Content.ReadFromJsonAsync<PessoaDetalheResponse>();

        var patchResponse = await client.PatchAsync($"/api/v1/pessoas/{created!.Id}/inativar", null);
        var updated = await patchResponse.Content.ReadFromJsonAsync<PessoaDetalheResponse>();

        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        updated.Should().NotBeNull();
        updated!.Ativo.Should().BeFalse();
    }

    private sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

    private sealed record PessoaResumoResponse(Guid Id, string Nome, string TipoPessoa, string? CpfCnpj, string? Email, string? Telefone, bool Ativo);

    private sealed record PessoaDetalheResponse(
        Guid Id,
        string Nome,
        string TipoPessoa,
        string? CpfCnpj,
        string? Email,
        string? Telefone,
        string? Observacao,
        bool Ativo);
}
