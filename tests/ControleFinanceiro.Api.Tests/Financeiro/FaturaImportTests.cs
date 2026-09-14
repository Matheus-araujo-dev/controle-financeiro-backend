using ControleFinanceiro.Application.Common.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ControleFinanceiro.Api.Tests.Infrastructure;
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

    [Fact]
    public async Task BradescoPdf_PreservaVencimentoParcelaEstornoEReimportacao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        string[] lines = ["Aplicativo Bradesco Cartoes", "Data: 03/04/2026 - 07:54", "Vencimento", "13/04/2026",
            "CLIENTE EXEMPLO - VISA", "XXXX.XXXX.XXXX.1111", "05/03", "MERCADO", "150,00", "05/03", "MERCADO", "150,00",
            "12/12", "LOJA TENIS", "4/6", "100,00", "20/03", "ESTORNO MERCADO", "-20,00",
            "13/03", "PAGTO. POR DEB EM C/C", "-230,00"];
        for (var n = 0; n < lines.Length; n++) page.AddText(lines[n], 10, new PdfPoint(40, 800 - n * 15), font);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(builder.Build());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "arquivo", "bradesco.pdf");
        var response = await client.PostAsync($"/api/v1/faturas/importar/preview?cartaoId={fixture.CartaoId}", form);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = (await response.Content.ReadFromJsonAsync<ImportacaoFaturaPreviewResponse>())!;
        preview.Itens.Should().HaveCount(4, preview.AvisoFormato);
        preview.Itens.Select(i => i.ChaveImportacao).Should().OnlyHaveUniqueItems();
        preview.ValorTotal.Should().Be(380m);
        preview.Itens.Should().OnlyContain(i => i.DataVencimentoFatura == new DateOnly(2026, 4, 13));
        preview.Itens.Should().Contain(i => i.NumeroParcela == 4 && i.QuantidadeParcelas == 6 && i.DataTransacao == new DateOnly(2025, 12, 12));
        preview.Itens.Should().Contain(i => i.Valor == -20m);
        var request = new ConfirmarImportacaoFaturaRequest(fixture.CartaoId, fixture.RecebedorId,
            preview.Itens.Select(i => new ImportacaoFaturaItemConfirmar(i.DataTransacao, i.Descricao, i.Valor,
                i.ChaveImportacao, null, i.DataVencimentoFatura, i.NumeroParcela, i.QuantidadeParcelas)).ToArray(),
            fixture.FormaPagamentoCartaoId, fixture.ContaGerencialDespesaId);
        var confirmed = await client.PostAsJsonAsync("/api/v1/faturas/importar/confirmar", request);
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());
        (await confirmed.Content.ReadFromJsonAsync<ConfirmarImportacaoFaturaResponse>())!.ContasCriadas.Should().Be(4);
        var repeated = await client.PostAsJsonAsync("/api/v1/faturas/importar/confirmar", request);
        (await repeated.Content.ReadFromJsonAsync<ConfirmarImportacaoFaturaResponse>())!.ContasDuplicadas.Should().Be(4);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var items = await db.ContasPagar.IgnoreQueryFilters().AsNoTracking().Where(i => i.CartaoId == fixture.CartaoId).ToListAsync();
        items.Should().HaveCount(4);
        items.Should().OnlyContain(i => i.DataVencimento == new DateOnly(2026, 4, 13));
        items.Sum(i => i.ValorLiquido).Should().Be(380m);
        items.Should().OnlyContain(i => i.StatusContaId == ControleFinanceiro.Domain.Financeiro.StatusConta.EmFaturaId);
        items.Should().Contain(i => i.NumeroParcela == 4 && i.QuantidadeParcelas == 6);

    }

    [Fact]
    public async Task Swagger_ExpoeMetadadosDaImportacaoBradesco()
    {
        using var client = _factory.CreateClient();
        var json = await client.GetStringAsync("/swagger/v1/swagger.json");
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var name in new[] { "ImportacaoFaturaItemPreview", "ImportacaoFaturaItemConfirmar" })
        {
            var properties = schemas.GetProperty(name).GetProperty("properties");
            properties.TryGetProperty("dataVencimentoFatura", out _).Should().BeTrue();
            properties.TryGetProperty("numeroParcela", out _).Should().BeTrue();
            properties.TryGetProperty("quantidadeParcelas", out _).Should().BeTrue();
        }
        if (Environment.GetEnvironmentVariable("CF_EXPORT_OPENAPI") is { Length: > 0 } destination)
            await File.WriteAllTextAsync(destination, json);
    }
}
