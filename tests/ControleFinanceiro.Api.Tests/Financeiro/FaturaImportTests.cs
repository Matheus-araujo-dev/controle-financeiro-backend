using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Financeiro.Importacao;
using System.Reflection;
using ControleFinanceiro.Contracts.Financeiro.ImportacaoFatura;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class FaturaImportTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task PreviewEConfirmar_CsvDeFatura_DeveCriarContas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        const string csv = "data,descricao,valor\n" +
                           "2026-04-05,Mercado,\"150,50\"\n" +
                           "2026-04-08,Posto,\"89,90\"\n" +
                           "2026-04-10,Farmacia,\"45,00\"";

        using var form = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(arquivo, "arquivo", "fatura.csv");

        var previewResp = await client.PostAsync(
            $"/api/v1/faturas/importar/preview?cartaoId={fixture.CartaoId}", form);
        previewResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = await previewResp.Content.ReadFromJsonAsync<ImportacaoFaturaPreviewResponse>();
        preview!.TotalItens.Should().Be(3);
        preview.ValorTotal.Should().Be(285.40m);

        var itens = preview.Itens
            .Where(i => !i.JaImportado)
            .Select(i => new ImportacaoFaturaItemConfirmar(i.DataTransacao, i.Descricao, i.Valor, i.ChaveImportacao))
            .ToList();

        var confirmarResp = await client.PostAsJsonAsync("/api/v1/faturas/importar/confirmar",
            new ConfirmarImportacaoFaturaRequest(
                CartaoId: fixture.CartaoId,
                FormaPagamentoId: fixture.FormaPagamentoCartaoId,
                RecebedorPadraoId: fixture.RecebedorId,
                ContaGerencialPadraoId: fixture.ContaGerencialDespesaId,
                Itens: itens));

        confirmarResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var resultado = await confirmarResp.Content.ReadFromJsonAsync<ConfirmarImportacaoFaturaResponse>();
        resultado!.ContasCriadas.Should().Be(3);

        // Reimportar o mesmo CSV deve detectar itens já importados
        using var form2 = new MultipartFormDataContent();
        var arquivo2 = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        arquivo2.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form2.Add(arquivo2, "arquivo", "fatura.csv");
        var preview2Resp = await client.PostAsync(
            $"/api/v1/faturas/importar/preview?cartaoId={fixture.CartaoId}", form2);
        var preview2 = await preview2Resp.Content.ReadFromJsonAsync<ImportacaoFaturaPreviewResponse>();
        preview2!.Itens.Should().OnlyContain(i => i.JaImportado);
    }

    [Fact]
    public void ParseJsonFromLlmResponse_QuandoIaRetornaTextoComJson_DeveExtrairObjeto()
    {
        var method = typeof(ImportacaoFaturaService).GetMethod(
            "ParseJsonFromLlmResponse",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();

        var node = method!.Invoke(null,
            ["Resultado da leitura:\n{\"transacoes\":[{\"data\":\"05/04/2026\",\"descricao\":\"Mercado PDF\",\"valor\":150.50}]}\nFim."])
            as System.Text.Json.Nodes.JsonNode;

        node.Should().NotBeNull();
        var item = node!["transacoes"]!.AsArray().Single()!;
        item["descricao"]!.GetValue<string>().Should().Be("Mercado PDF");
        item["valor"]!.GetValue<decimal>().Should().Be(150.50m);
    }

    [Fact]
    public async Task Preview_SemArquivo_DeveRetornar400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        using var form = new MultipartFormDataContent();
        var vazio = new ByteArrayContent([]);
        vazio.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(vazio, "arquivo", "vazio.csv");

        var resp = await client.PostAsync($"/api/v1/faturas/importar/preview?cartaoId={fixture.CartaoId}", form);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
