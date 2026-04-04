using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public sealed class FaturaCartaoTests
{
    [Fact]
    public void Pagar_DeveRegistrarContaStatusEDataPagamento()
    {
        var fatura = FaturaCartao.Criar(
            cartaoId: Guid.NewGuid(),
            competencia: "2026-04",
            dataFechamento: new DateOnly(2026, 4, 10),
            dataVencimento: new DateOnly(2026, 4, 20),
            valorTotal: 150m,
            observacao: "Fatura abril");

        var contaBancariaId = Guid.NewGuid();

        fatura.Pagar(new DateOnly(2026, 4, 20), contaBancariaId, "Pagamento integral");

        fatura.Status.Should().Be(StatusFaturaCartao.Paga);
        fatura.DataPagamento.Should().Be(new DateOnly(2026, 4, 20));
        fatura.ContaBancariaPagamentoId.Should().Be(contaBancariaId);
        fatura.Observacao.Should().Be("Pagamento integral");
    }

    [Fact]
    public void Pagar_QuandoJaPaga_DeveFalhar()
    {
        var fatura = FaturaCartao.Criar(
            cartaoId: Guid.NewGuid(),
            competencia: "2026-04",
            dataFechamento: new DateOnly(2026, 4, 10),
            dataVencimento: new DateOnly(2026, 4, 20),
            valorTotal: 150m,
            observacao: null);

        fatura.Pagar(new DateOnly(2026, 4, 20), Guid.NewGuid(), null);

        var action = () => fatura.Pagar(new DateOnly(2026, 4, 21), Guid.NewGuid(), null);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ja foi paga*");
    }
}
