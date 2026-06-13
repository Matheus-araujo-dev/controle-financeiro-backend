using ControleFinanceiro.Application.FinanceAI;
using ControleFinanceiro.Infrastructure.FinanceAI;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControleFinanceiro.Infrastructure.Tests.FinanceAI;

public sealed class ClaudeVisionExtracaoServiceTests
{
    private sealed class FakeVisionClient(string? resposta) : ILlmVisionClient
    {
        public Task<string?> AnalisarImagemAsync(
            string systemPrompt, string userText, string imagemBase64,
            string mimeType, CancellationToken cancellationToken)
            => Task.FromResult(resposta);
    }

    private static ClaudeVisionExtracaoService CriarServico(string? respostaLlm) =>
        new(new FakeVisionClient(respostaLlm),
            NullLogger<ClaudeVisionExtracaoService>.Instance);

    [Fact]
    public async Task ExtrairAsync_ComRespostaValida_DeveRetornarDadosCorretos()
    {
        const string json = """
            {"sucesso":true,"estabelecimento":"Mercado Central","valor":89.90,"data":"2026-06-10","descricao":"Compras do mês"}
            """;

        var resultado = await CriarServico(json).ExtrairAsync("base64fake", "image/jpeg", CancellationToken.None);

        resultado.Sucesso.Should().BeTrue();
        resultado.Estabelecimento.Should().Be("Mercado Central");
        resultado.Valor.Should().Be(89.90m);
        resultado.Data.Should().Be(new DateOnly(2026, 6, 10));
        resultado.Descricao.Should().Be("Compras do mês");
    }

    [Fact]
    public async Task ExtrairAsync_ComSucessoFalso_DeveRetornarFalhou()
    {
        var resultado = await CriarServico("""{"sucesso":false}""")
            .ExtrairAsync("base64fake", "image/jpeg", CancellationToken.None);

        resultado.Sucesso.Should().BeFalse();
    }

    [Fact]
    public async Task ExtrairAsync_ComRespostaNula_DeveRetornarFalhou()
    {
        var resultado = await CriarServico(null)
            .ExtrairAsync("base64fake", "image/jpeg", CancellationToken.None);

        resultado.Sucesso.Should().BeFalse();
    }

    [Fact]
    public async Task ExtrairAsync_ComJsonEmMarkdown_DeveExtrairCorretamente()
    {
        const string resposta = """
            ```json
            {"sucesso":true,"estabelecimento":"Loja XYZ","valor":45.00,"data":"2026-01-15","descricao":"Produto A"}
            ```
            """;

        var resultado = await CriarServico(resposta)
            .ExtrairAsync("base64fake", "image/jpeg", CancellationToken.None);

        resultado.Sucesso.Should().BeTrue();
        resultado.Estabelecimento.Should().Be("Loja XYZ");
        resultado.Valor.Should().Be(45.00m);
    }

    [Fact]
    public async Task ExtrairAsync_ComDataInvalida_DeveRetornarDataNula()
    {
        const string json = """{"sucesso":true,"estabelecimento":"X","valor":10.0,"data":"invalida","descricao":"Y"}""";

        var resultado = await CriarServico(json)
            .ExtrairAsync("base64fake", "image/jpeg", CancellationToken.None);

        resultado.Sucesso.Should().BeTrue();
        resultado.Data.Should().BeNull();
        resultado.Valor.Should().Be(10.0m);
    }

    [Fact]
    public async Task ExtrairAsync_SemCampoData_DeveRetornarDataNula()
    {
        const string json = """{"sucesso":true,"estabelecimento":"Bar","valor":25.50,"descricao":"Almoço"}""";

        var resultado = await CriarServico(json)
            .ExtrairAsync("base64fake", "image/jpeg", CancellationToken.None);

        resultado.Sucesso.Should().BeTrue();
        resultado.Data.Should().BeNull();
        resultado.Valor.Should().Be(25.50m);
    }
}
