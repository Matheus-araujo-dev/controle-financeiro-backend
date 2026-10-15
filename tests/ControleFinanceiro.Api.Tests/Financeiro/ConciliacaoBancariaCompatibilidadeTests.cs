using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Contracts.Conciliacao;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class ConciliacaoBancariaCompatibilidadeTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Theory]
    [InlineData("extrato.csv", "Data;Descricao;Valor;Documento\n01/09/2026;Mercado;-10,50;123\n02/09/2026;Credito;20,00;")]
    [InlineData("extrato.ofx", "<OFX><BANKTRANLIST><STMTTRN><DTPOSTED>20260901</DTPOSTED><TRNAMT>-10.50</TRNAMT><MEMO>Mercado</MEMO><CHECKNUM>123</CHECKNUM></STMTTRN><STMTTRN><DTPOSTED>20260902</DTPOSTED><TRNAMT>20.00</TRNAMT><MEMO>Credito</MEMO></STMTTRN></BANKTRANLIST></OFX>")]
    public async Task FluxoBancario_PreservaImportacaoListaEDecisoes(string nome, string texto)
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(fixture.ContaBancariaId.ToString()), "contaBancariaId");
        form.Add(new StringContent(texto), "arquivo", nome);
        var result = await client.PostAsync("/api/v1/conciliacoes", form);
        result.StatusCode.Should().Be(HttpStatusCode.Created, await result.Content.ReadAsStringAsync());
        var session = (await result.Content.ReadFromJsonAsync<ConciliacaoDetalheResponse>())!;
        session.Itens.OrderBy(x => x.Data).Select(x => x.Valor).Should().Equal(-10.50m, 20m);
        var lista = (await client.GetFromJsonAsync<ConciliacaoResumoResponse[]>("/api/v1/conciliacoes"))!;
        lista.Should().ContainSingle(x => x.Id == session.Id && x.ContaBancariaId == fixture.ContaBancariaId);
        var itens = session.Itens.OrderBy(x => x.Data).ToArray();
        (await client.PatchAsJsonAsync($"/api/v1/conciliacoes/{session.Id}/itens/{itens[0].Id}/conciliar", new { movimentacaoId = (Guid?)null }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PatchAsync($"/api/v1/conciliacoes/{session.Id}/itens/{itens[1].Id}/ignorar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var final = (await client.GetFromJsonAsync<ConciliacaoDetalheResponse>($"/api/v1/conciliacoes/{session.Id}"))!;
        final.Status.Should().Be("Concluida");
        final.Itens.Should().Contain(x => x.Status == "Conciliado");
        final.Itens.Should().Contain(x => x.Status == "Ignorado");
        (await client.GetAsync($"/api/v1/faturas/{Guid.NewGuid()}/conciliacoes/{session.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
