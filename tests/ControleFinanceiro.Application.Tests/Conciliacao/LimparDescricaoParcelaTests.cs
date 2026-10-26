using ControleFinanceiro.Application.Conciliacao;
using FluentAssertions;
namespace ControleFinanceiro.Application.Tests.Conciliacao;
public sealed class LimparDescricaoParcelaTests
{
    [Theory]
    [InlineData("mercado livre*mercadolivre parcela 1 de 2", "mercado livre*mercadolivre")]
    [InlineData("MERCADO LIVRE*MERCADOLIVRE PARCELA 1 DE 2", "MERCADO LIVRE*MERCADOLIVRE")]
    [InlineData("Loja XYZ Parcela 3 de 12", "Loja XYZ")]
    [InlineData("compra normal sem parcela", "compra normal sem parcela")]
    [InlineData("parcela 1 de 1", "")]
    [InlineData("ASAAS*NEXTFIT parcela 10 de 12", "ASAAS*NEXTFIT")]
    public void Remove_SufixoParcela(string input, string expected)
    {
        ConciliacaoFaturaAppService.LimparDescricaoParcela(input).Should().Be(expected);
    }
}
