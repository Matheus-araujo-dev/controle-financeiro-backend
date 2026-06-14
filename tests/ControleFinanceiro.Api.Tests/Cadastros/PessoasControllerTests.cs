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
            observacao = "Cadastro inicial",
            chavesPix = new[]
            {
                new
                {
                    tipo = "Email",
                    chave = "pix@example.com"
                },
                new
                {
                    tipo = "Telefone",
                    chave = "(11) 98889-1273"
                }
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<PessoaDetalheResponse>();
        var listResponse = await client.GetFromJsonAsync<PagedResponse<PessoaResumoResponse>>("/api/v1/pessoas?search=fornecedor");

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Should().NotBeNull();
        created!.CpfCnpj.Should().Be("12345678000190");
        created.ChavesPix.Should().ContainSingle(item => item.Tipo == "Email" && item.Chave == "pix@example.com");
        created.ChavesPix.Should().ContainSingle(item => item.Tipo == "Telefone" && item.Chave == "11988891273");
        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().ContainSingle(item =>
            item.Nome == "Fornecedor Exemplo" &&
            item.CpfCnpj == "12345678000190");
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

    [Fact]
    public async Task Put_DeveAtualizarChavesPixDaPessoa()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/v1/pessoas", new
        {
            nome = "Cliente Exemplo",
            tipoPessoa = "Fisica",
            chavesPix = new[]
            {
                new
                {
                    tipo = "Email",
                    chave = "pix@example.com"
                }
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<PessoaDetalheResponse>();

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/pessoas/{created!.Id}", new
        {
            nome = "Cliente Exemplo",
            tipoPessoa = "Fisica",
            cpfCnpj = (string?)null,
            email = (string?)null,
            telefone = (string?)null,
            observacao = "Atualizada",
            chavesPix = new[]
            {
                new
                {
                    tipo = "CpfCnpj",
                    chave = "437.782.098-25"
                },
                new
                {
                    tipo = "Aleatoria",
                    chave = "550e8400-e29b-41d4-a716-446655440000"
                }
            }
        });

        var updated = await updateResponse.Content.ReadFromJsonAsync<PessoaDetalheResponse>();

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        updated.Should().NotBeNull();
        updated!.Observacao.Should().Be("Atualizada");
        updated.ChavesPix.Should().HaveCount(2);
        updated.ChavesPix.Should().ContainSingle(item => item.Tipo == "CpfCnpj" && item.Chave == "43778209825");
        updated.ChavesPix.Should().ContainSingle(item => item.Tipo == "Aleatoria" && item.Chave == "550e8400-e29b-41d4-a716-446655440000");
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
        bool Ativo,
        IReadOnlyCollection<PessoaChavePixResponse> ChavesPix);

    private sealed record PessoaChavePixResponse(string Tipo, string Chave);
}
