using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Contracts.Cadastros.Cartoes;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Cadastros;

public sealed class CartoesCrudTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task ObterPorIdEAtualizar_DeveRefletirMudancas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var detalhe = await client.GetFromJsonAsync<CartaoDetalheResponse>($"/api/v1/cartoes/{fixture.CartaoId}");
        detalhe!.Id.Should().Be(fixture.CartaoId);

        var atualizar = await client.PutAsJsonAsync($"/api/v1/cartoes/{fixture.CartaoId}",
            new AtualizarCartaoRequest(
                Nome: "Cartão Renomeado",
                Bandeira: "Mastercard",
                NumeroFinal: "4321",
                DiaFechamentoFatura: 10,
                DiaVencimentoFatura: 20,
                ContaBancariaPagamentoPadraoId: fixture.ContaBancariaId,
                LimiteCredito: 9000m,
                Ativo: true));
        atualizar.StatusCode.Should().Be(HttpStatusCode.OK);
        var atualizado = await atualizar.Content.ReadFromJsonAsync<CartaoDetalheResponse>();
        atualizado!.Nome.Should().Be("Cartão Renomeado");
        atualizado.DiaFechamentoFatura.Should().Be(10);
    }

    [Fact]
    public async Task Criar_NovoCartao_DeveRetornarCreated()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var criar = await client.PostAsJsonAsync("/api/v1/cartoes", new CriarCartaoRequest(
            Nome: "Cartão Extra",
            Bandeira: "Visa",
            NumeroFinal: "1234",
            DiaFechamentoFatura: 5,
            DiaVencimentoFatura: 15,
            ContaBancariaPagamentoPadraoId: fixture.ContaBancariaId,
            LimiteCredito: 3000m,
            Ativo: true));

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        var criado = await criar.Content.ReadFromJsonAsync<CartaoDetalheResponse>();
        criado!.Nome.Should().Be("Cartão Extra");
    }

    [Fact]
    public async Task ObterPorId_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resposta = await client.GetAsync($"/api/v1/cartoes/{Guid.NewGuid()}");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
