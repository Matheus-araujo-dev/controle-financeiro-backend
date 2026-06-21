using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Contracts.Cadastros.ContasBancarias;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Cadastros;

public sealed class ContasBancariasCrudTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static CriarContaBancariaRequest Nova(string nome = "Conta Corrente") => new(
        Nome: nome,
        Banco: "Banco Teste",
        Agencia: "0001",
        NumeroConta: "12345-6",
        TipoConta: "Corrente",
        SaldoInicial: 1000m,
        DataSaldoInicial: new DateOnly(2026, 1, 1),
        LimiteCartoesCompartilhado: 5000m,
        Ativo: true);

    [Fact]
    public async Task Crud_CriarObterAtualizarEListar()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var criar = await client.PostAsJsonAsync("/api/v1/contas-bancarias", Nova());
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        var criada = await criar.Content.ReadFromJsonAsync<ContaBancariaDetalheResponse>();
        criada!.Nome.Should().Be("Conta Corrente");
        criada.SaldoInicial.Should().Be(1000m);

        var detalhe = await client.GetFromJsonAsync<ContaBancariaDetalheResponse>(
            $"/api/v1/contas-bancarias/{criada.Id}");
        detalhe!.Banco.Should().Be("Banco Teste");
        detalhe.LimiteCartoesCompartilhado.Should().Be(5000m);

        var atualizar = await client.PutAsJsonAsync($"/api/v1/contas-bancarias/{criada.Id}",
            new AtualizarContaBancariaRequest(
                Nome: "Conta Poupança",
                Banco: "Banco Novo",
                Agencia: "0002",
                NumeroConta: "65432-1",
                TipoConta: "Poupanca",
                SaldoInicial: 2000m,
                DataSaldoInicial: new DateOnly(2026, 1, 1),
                LimiteCartoesCompartilhado: null,
                Ativo: false));
        atualizar.StatusCode.Should().Be(HttpStatusCode.OK);
        var atualizada = await atualizar.Content.ReadFromJsonAsync<ContaBancariaDetalheResponse>();
        atualizada!.Nome.Should().Be("Conta Poupança");
        atualizada.SaldoInicial.Should().Be(2000m);

        var lista = await client.GetFromJsonAsync<ContaBancariaListResponse>("/api/v1/contas-bancarias");
        lista!.Items.Should().Contain(c => c.Id == criada.Id);
    }

    [Fact]
    public async Task ObterPorId_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resposta = await client.GetAsync($"/api/v1/contas-bancarias/{Guid.NewGuid()}");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Atualizar_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resposta = await client.PutAsJsonAsync($"/api/v1/contas-bancarias/{Guid.NewGuid()}",
            new AtualizarContaBancariaRequest(
                Nome: "X", Banco: "Y", Agencia: null, NumeroConta: null, TipoConta: null,
                SaldoInicial: 0m, DataSaldoInicial: new DateOnly(2026, 1, 1),
                LimiteCartoesCompartilhado: null, Ativo: true));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
