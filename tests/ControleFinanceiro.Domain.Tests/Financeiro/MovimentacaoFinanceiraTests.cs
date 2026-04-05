using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public sealed class MovimentacaoFinanceiraTests
{
    [Fact]
    public void Conciliar_DeveAtualizarStatusDataEObservacao()
    {
        var movimentacao = MovimentacaoFinanceira.CriarLiquidacaoContaReceber(
            contaReceberId: Guid.NewGuid(),
            contaBancariaId: Guid.NewGuid(),
            dataMovimentacao: new DateOnly(2026, 4, 8),
            valor: 80m,
            statusMovimentacaoId: StatusMovimentacao.EfetivadaId,
            observacao: "Recebimento cliente");

        movimentacao.Conciliar(new DateOnly(2026, 4, 9), StatusMovimentacao.ConciliadaId, "Conciliado com item do extrato");

        movimentacao.StatusMovimentacaoId.Should().Be(StatusMovimentacao.ConciliadaId);
        movimentacao.DataConciliacao.Should().Be(new DateOnly(2026, 4, 9));
        movimentacao.Observacao.Should().Be("Conciliado com item do extrato");
    }
}
