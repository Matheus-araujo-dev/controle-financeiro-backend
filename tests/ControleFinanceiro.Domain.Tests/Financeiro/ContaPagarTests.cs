using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public sealed class ContaPagarTests
{
    [Fact]
    public void Criar_QuandoRateiosNaoFecharemValorLiquido_DeveFalhar()
    {
        var rateios = new[]
        {
            RateioPlano.Create(Guid.NewGuid(), 50m),
            RateioPlano.Create(Guid.NewGuid(), 49.99m)
        };

        var action = () => ContaPagar.Criar(
            numeroDocumento: "NF-100",
            dataEmissao: new DateOnly(2026, 4, 4),
            responsavelCompraId: null,
            recebedorId: Guid.NewGuid(),
            dataVencimento: new DateOnly(2026, 4, 10),
            formaPagamentoId: Guid.NewGuid(),
            cartaoId: null,
            contaBancariaId: null,
            valorOriginal: 100m,
            valorDesconto: 0m,
            valorJuros: 0m,
            valorMulta: 0m,
            quantidadeParcelas: 1,
            numeroParcela: 1,
            grupoParcelamentoId: null,
            descricao: "Fornecedor",
            observacao: null,
            statusContaId: StatusConta.PendenteId,
            ehRecorrente: false,
            regraRecorrenciaId: null,
            origem: OrigemLancamento.Manual,
            rateios: rateios);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("rateios");
    }

    [Fact]
    public void CriarParcelas_DeveDistribuirCentavosSemPerderFechamento()
    {
        var parcelas = ContaPagar.CriarParcelas(
            numeroDocumento: "NF-200",
            dataEmissao: new DateOnly(2026, 4, 4),
            responsavelCompraId: null,
            recebedorId: Guid.NewGuid(),
            dataVencimento: new DateOnly(2026, 4, 10),
            formaPagamentoId: Guid.NewGuid(),
            cartaoId: null,
            contaBancariaId: null,
            valorOriginal: 100m,
            valorDesconto: 0m,
            valorJuros: 0m,
            valorMulta: 0.01m,
            quantidadeParcelas: 3,
            descricao: "Parcela",
            observacao: null,
            statusContaId: StatusConta.PendenteId,
            ehRecorrente: false,
            regraRecorrenciaId: null,
            origem: OrigemLancamento.Manual,
            rateios: new[]
            {
                RateioPlano.Create(Guid.NewGuid(), 60.01m),
                RateioPlano.Create(Guid.NewGuid(), 40m)
            });

        parcelas.Should().HaveCount(3);
        parcelas.Select(x => x.NumeroParcela).Should().ContainInOrder(1, 2, 3);
        parcelas.Select(x => x.GrupoParcelamentoId).Distinct().Should().HaveCount(1);
        parcelas.Sum(x => x.ValorLiquido).Should().Be(100.01m);
        parcelas.Last().ValorLiquido.Should().Be(33.35m);
        parcelas.All(x => x.Rateios.Sum(rateio => rateio.Valor) == x.ValorLiquido).Should().BeTrue();
    }

    [Fact]
    public void Liquidar_DeveAtualizarStatusDataEContaBancaria()
    {
        var conta = ContaPagar.Criar(
            numeroDocumento: null,
            dataEmissao: new DateOnly(2026, 4, 4),
            responsavelCompraId: null,
            recebedorId: Guid.NewGuid(),
            dataVencimento: new DateOnly(2026, 4, 10),
            formaPagamentoId: Guid.NewGuid(),
            cartaoId: null,
            contaBancariaId: null,
            valorOriginal: 100m,
            valorDesconto: 5m,
            valorJuros: 0m,
            valorMulta: 0m,
            quantidadeParcelas: 1,
            numeroParcela: 1,
            grupoParcelamentoId: null,
            descricao: "Liquidação",
            observacao: null,
            statusContaId: StatusConta.PendenteId,
            ehRecorrente: false,
            regraRecorrenciaId: null,
            origem: OrigemLancamento.Manual,
            rateios: new[]
            {
                RateioPlano.Create(Guid.NewGuid(), 95m)
            });

        var contaBancariaId = Guid.NewGuid();

        conta.Liquidar(new DateOnly(2026, 4, 6), contaBancariaId, StatusConta.LiquidadaId);

        conta.StatusContaId.Should().Be(StatusConta.LiquidadaId);
        conta.DataLiquidacao.Should().Be(new DateOnly(2026, 4, 6));
        conta.ContaBancariaId.Should().Be(contaBancariaId);
    }
}
