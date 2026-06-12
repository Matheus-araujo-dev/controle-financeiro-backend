using System.Text.Json;
using ControleFinanceiro.Application.ImportacoesWhatsapp;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;
using FluentAssertions;

namespace ControleFinanceiro.Infrastructure.Tests.ImportacoesWhatsapp;

public sealed class HeuristicImportSuggestionServiceTests
{
    [Fact]
    public async Task GenerateAsync_WhenTextContainsNormalizedCardInvoiceItems_ShouldCreateOneSuggestionPerTransaction()
    {
        var service = new HeuristicImportSuggestionService();

        var suggestions = await service.GenerateAsync(
            new ImportSuggestionRequest(
                TipoOrigemImportacaoWhatsapp.Pdf,
                "5511999999999",
                """
                DOCUMENTO|FATURA_CARTAO
                EMISSOR|NUBANK
                TITULAR|CLIENTE EXEMPLO
                VENCIMENTO|2026-04-13
                ITEM|2026-03-07|Skalla|100,00|PORTADOR=Cliente Exemplo|CARTAO_FINAL=4835
                ITEM|2026-03-14|Estorno de Uber - NuPay|-12,96|PORTADOR=Cliente Exemplo|ESTORNO=true
                """,
                "fatura.pdf",
                "application/pdf"),
            CancellationToken.None);

        suggestions.Should().HaveCount(2);
        suggestions.Should().OnlyContain(item => item.TipoSugestao == TipoSugestaoImportacaoWhatsapp.CompraCartao);

        var payloads = suggestions
            .Select(item => JsonDocument.Parse(item.PayloadSugeridoJson).RootElement)
            .ToArray();

        payloads[0].GetProperty("descricao").GetString().Should().Be("Skalla");
        payloads[0].GetProperty("valor").GetDecimal().Should().Be(100.00m);
        payloads[0].GetProperty("dataIdentificada").GetString().Should().Be("2026-03-07");
        payloads[0].GetProperty("emissor").GetString().Should().Be("NUBANK");
        payloads[0].GetProperty("cartaoFinal").GetString().Should().Be("4835");
        payloads[0].GetProperty("portador").GetString().Should().Be("Cliente Exemplo");
        payloads[0].GetProperty("tipoMovimentacaoSugerido").GetString().Should().Be("Saida");
        payloads[0].TryGetProperty("textoExtraido", out _).Should().BeFalse();

        payloads[1].GetProperty("descricao").GetString().Should().Be("Estorno de Uber - NuPay");
        payloads[1].GetProperty("valor").GetDecimal().Should().Be(-12.96m);
        payloads[1].GetProperty("ehEstorno").GetBoolean().Should().BeTrue();
        payloads[1].GetProperty("tipoMovimentacaoSugerido").GetString().Should().Be("Entrada");
        payloads[1].TryGetProperty("textoExtraido", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_WhenCreatesGenericSuggestion_ShouldNotDuplicateExtractedTextInPayload()
    {
        var service = new HeuristicImportSuggestionService();

        var suggestions = await service.GenerateAsync(
            new ImportSuggestionRequest(
                TipoOrigemImportacaoWhatsapp.Texto,
                "5511999999999",
                "Pagar academia 120,50 em 2026-04-05",
                null,
                null),
            CancellationToken.None);

        suggestions.Should().HaveCount(1);

        var payload = JsonDocument.Parse(suggestions.Single().PayloadSugeridoJson).RootElement;
        payload.GetProperty("descricao").GetString().Should().Be("Pagar academia 120,50 em 2026-04-05");
        payload.GetProperty("valor").GetDecimal().Should().Be(120.50m);
        payload.GetProperty("dataIdentificada").GetString().Should().Be("2026-04-05");
        payload.TryGetProperty("textoExtraido", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_WhenInvoiceContainsAmountAboveThousand_ShouldParseFullAmount()
    {
        var service = new HeuristicImportSuggestionService();

        var suggestions = await service.GenerateAsync(
            new ImportSuggestionRequest(
                TipoOrigemImportacaoWhatsapp.Pdf,
                "bradesco-modelo",
                """
                DOCUMENTO|FATURA_CARTAO
                EMISSOR|BRADESCO
                TITULAR|CLIENTE EXEMPLO
                ITEM|2025-12-12|JIM.COM 30748782 MATHEUS 4/4|1073,53|PORTADOR=CLIENTE EXEMPLO|CARTAO_FINAL=2892|PARCELA=4/4
                """,
                "fatura.pdf",
                "application/pdf"),
            CancellationToken.None);

        suggestions.Should().HaveCount(1);

        var payload = JsonDocument.Parse(suggestions.Single().PayloadSugeridoJson).RootElement;
        payload.GetProperty("descricao").GetString().Should().Be("JIM.COM 30748782 MATHEUS 4/4");
        payload.GetProperty("valor").GetDecimal().Should().Be(1073.53m);
        payload.GetProperty("parcela").GetString().Should().Be("4/4");
    }
}
