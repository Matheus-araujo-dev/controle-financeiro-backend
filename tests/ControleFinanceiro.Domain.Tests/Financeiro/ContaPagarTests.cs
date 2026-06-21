using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public sealed class ContaPagarTests
{
    [Fact]
    public void Criar_QuandoCompraEmCartaoTiverEstorno_DevePermitirValorLiquidoNegativo()
    {
        var conta = ContaPagar.Criar(
            numeroDocumento: null,
            dataEmissao: new DateOnly(2026, 4, 4),
            responsavelCompraId: null,
            recebedorId: Guid.NewGuid(),
            dataVencimento: new DateOnly(2026, 4, 13),
            formaPagamentoId: Guid.NewGuid(),
            cartaoId: Guid.NewGuid(),
            contaBancariaId: null,
            valorOriginal: -12.96m,
            valorDesconto: 0m,
            valorJuros: 0m,
            valorMulta: 0m,
            quantidadeParcelas: 1,
            numeroParcela: 1,
            grupoParcelamentoId: null,
            origemCompraPlanejadaId: null,
            descricao: "Estorno no cartao",
            observacao: null,
            statusContaId: StatusConta.PendenteId,
            ehRecorrente: false,
            regraRecorrenciaId: null,
            origem: OrigemLancamento.Importacao,
            rateios: new[]
            {
                RateioPlano.CreateSigned(Guid.NewGuid(), -12.96m)
            });

        conta.ValorLiquido.Should().Be(-12.96m);
        conta.Rateios.Should().ContainSingle();
        conta.Rateios.Single().Valor.Should().Be(-12.96m);
    }

    [Fact]
    public void Criar_QuandoContaOperacionalTiverValorNegativo_DeveFalhar()
    {
        var action = () => ContaPagar.Criar(
            numeroDocumento: null,
            dataEmissao: new DateOnly(2026, 4, 4),
            responsavelCompraId: null,
            recebedorId: Guid.NewGuid(),
            dataVencimento: new DateOnly(2026, 4, 10),
            formaPagamentoId: Guid.NewGuid(),
            cartaoId: null,
            contaBancariaId: null,
            valorOriginal: -10m,
            valorDesconto: 0m,
            valorJuros: 0m,
            valorMulta: 0m,
            quantidadeParcelas: 1,
            numeroParcela: 1,
            grupoParcelamentoId: null,
            origemCompraPlanejadaId: null,
            descricao: "Conta negativa",
            observacao: null,
            statusContaId: StatusConta.PendenteId,
            ehRecorrente: false,
            regraRecorrenciaId: null,
            origem: OrigemLancamento.Manual,
            rateios: new[]
            {
                RateioPlano.CreateSigned(Guid.NewGuid(), -10m)
            });

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Valor liquido deve ser maior que zero para contas operacionais.*");
    }

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
            origemCompraPlanejadaId: null,
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
            origemCompraPlanejadaId: null,
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
    public void CriarParcelasCartao_DeveAtualizarDescricaoComNumeroDaParcelaCorrente()
    {
        var parcelas = ContaPagar.CriarParcelasCartao(
            numeroDocumento: "NF-300",
            dataEmissao: new DateOnly(2026, 4, 8),
            responsavelCompraId: null,
            recebedorId: Guid.NewGuid(),
            formaPagamentoId: Guid.NewGuid(),
            cartaoId: Guid.NewGuid(),
            valorOriginal: 300m,
            valorDesconto: 0m,
            valorJuros: 0m,
            valorMulta: 0m,
            quantidadeParcelas: 3,
            origemCompraPlanejadaId: null,
            descricao: "Cadeira DT3 ErgoOne 1/3",
            observacao: null,
            statusContaId: StatusConta.PendenteId,
            ehRecorrente: false,
            regraRecorrenciaId: null,
            origem: OrigemLancamento.Manual,
            rateios:
            [
                RateioPlano.Create(Guid.NewGuid(), 300m)
            ],
            diaFechamentoFatura: 5,
            diaVencimentoFatura: 15);

        parcelas.Select(x => x.Descricao).Should().ContainInOrder(
            "Cadeira DT3 ErgoOne 1/3",
            "Cadeira DT3 ErgoOne 2/3",
            "Cadeira DT3 ErgoOne 3/3");
        parcelas.Select(x => x.NumeroParcela).Should().ContainInOrder(1, 2, 3);
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
            origemCompraPlanejadaId: null,
            descricao: "Liquidacao",
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

    [Fact]
    public void Atualizar_QuandoContaEstiverLiquidada_DeveFalhar()
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
            valorDesconto: 0m,
            valorJuros: 0m,
            valorMulta: 0m,
            quantidadeParcelas: 1,
            numeroParcela: 1,
            grupoParcelamentoId: null,
            origemCompraPlanejadaId: null,
            descricao: "Atualizacao bloqueada",
            observacao: null,
            statusContaId: StatusConta.PendenteId,
            ehRecorrente: false,
            regraRecorrenciaId: null,
            origem: OrigemLancamento.Manual,
            rateios: new[]
            {
                RateioPlano.Create(Guid.NewGuid(), 100m)
            });

        conta.Liquidar(new DateOnly(2026, 4, 6), Guid.NewGuid(), StatusConta.LiquidadaId);

        var action = () => conta.Atualizar(
            numeroDocumento: null,
            dataEmissao: new DateOnly(2026, 4, 4),
            responsavelCompraId: null,
            recebedorId: conta.RecebedorId,
            dataVencimento: new DateOnly(2026, 4, 10),
            formaPagamentoId: conta.FormaPagamentoId,
            cartaoId: null,
            contaBancariaId: null,
            valorOriginal: 100m,
            valorDesconto: 0m,
            valorJuros: 0m,
            valorMulta: 0m,
            descricao: "Atualizacao bloqueada",
            observacao: null,
            statusContaId: StatusConta.PendenteId,
            rateios: new[]
            {
                RateioPlano.Create(conta.Rateios.Single().ContaGerencialId, 100m)
            });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Não é permitido editar contas liquidadas ou canceladas.*");
    }
}
