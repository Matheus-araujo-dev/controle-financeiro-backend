using ControleFinanceiro.Application.FinanceAI;
using ControleFinanceiro.Infrastructure.FinanceAI;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControleFinanceiro.Infrastructure.Tests.FinanceAI;

public sealed class ClaudeVisionExtracaoPagamentoTests
{
    [Fact]
    public async Task ExtrairAsync_DeveRetornarMeioPagamentoParcelasEIdentificacaoDoCartao()
    {
        const string json = """
            {
              "sucesso": true,
              "estabelecimento": "Loja Exemplo",
              "valor": 1200.00,
              "data": "2026-06-22",
              "descricao": "Notebook",
              "meioPagamento": "CartaoCredito",
              "quantidadeParcelas": 10,
              "finalCartao": "4242",
              "bandeiraCartao": "Visa"
            }
            """;
        var service = new ClaudeVisionExtracaoService(
            new FakeVisionClient(json),
            NullLogger<ClaudeVisionExtracaoService>.Instance);

        var result = await service.ExtrairAsync("base64", "image/jpeg", CancellationToken.None);

        result.Sucesso.Should().BeTrue();
        result.MeioPagamento.Should().Be("CartaoCredito");
        result.QuantidadeParcelas.Should().Be(10);
        result.FinalCartao.Should().Be("4242");
        result.BandeiraCartao.Should().Be("Visa");
    }

    private sealed class FakeVisionClient(string response) : ILlmVisionClient
    {
        public Task<string?> AnalisarImagemAsync(
            string systemPrompt,
            string userText,
            string imagemBase64,
            string mimeType,
            CancellationToken cancellationToken) => Task.FromResult<string?>(response);
    }
}
