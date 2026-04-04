using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public sealed class ContaReceberTests
{
    [Fact]
    public void Cancelar_QuandoContaEstiverLiquidada_DeveFalhar()
    {
        var conta = ContaReceber.Criar(
            numeroDocumento: "REC-1",
            dataEmissao: new DateOnly(2026, 4, 4),
            responsavelId: null,
            pagadorId: Guid.NewGuid(),
            dataVencimento: new DateOnly(2026, 4, 12),
            formaPagamentoId: Guid.NewGuid(),
            cartaoId: null,
            contaBancariaId: null,
            valorOriginal: 80m,
            valorDesconto: 0m,
            valorJuros: 0m,
            valorMulta: 0m,
            quantidadeParcelas: 1,
            numeroParcela: 1,
            grupoParcelamentoId: null,
            descricao: "Recebimento",
            observacao: null,
            statusContaId: StatusConta.PendenteId,
            ehRecorrente: false,
            regraRecorrenciaId: null,
            origem: OrigemLancamento.Manual,
            rateios: new[]
            {
                RateioPlano.Create(Guid.NewGuid(), 80m)
            });

        conta.Liquidar(new DateOnly(2026, 4, 5), Guid.NewGuid(), StatusConta.LiquidadaId);

        var action = () => conta.Cancelar(StatusConta.CanceladaId);

        action.Should().Throw<InvalidOperationException>();
    }
}
