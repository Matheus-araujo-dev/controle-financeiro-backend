using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public sealed class ContaReceberTests
{
    private static ContaReceber CriarConta(decimal valorOriginal = 80m, Guid? statusId = null) =>
        ContaReceber.Criar(
            numeroDocumento: "REC-1",
            dataEmissao: new DateOnly(2026, 4, 4),
            responsavelId: null,
            pagadorId: Guid.NewGuid(),
            dataVencimento: new DateOnly(2026, 4, 12),
            formaPagamentoId: Guid.NewGuid(),
            cartaoId: null,
            contaBancariaId: null,
            valorOriginal: valorOriginal,
            valorDesconto: 0m,
            valorJuros: 0m,
            valorMulta: 0m,
            quantidadeParcelas: 1,
            numeroParcela: 1,
            grupoParcelamentoId: null,
            descricao: "Recebimento",
            observacao: null,
            statusContaId: statusId ?? StatusConta.PendenteId,
            ehRecorrente: false,
            regraRecorrenciaId: null,
            origem: OrigemLancamento.Manual,
            rateios: new[] { RateioPlano.Create(Guid.NewGuid(), valorOriginal) });

    [Fact]
    public void Criar_ComDadosValidos_DeveDefinirPropriedades()
    {
        var conta = CriarConta(100m);

        conta.ValorOriginal.Should().Be(100m);
        conta.ValorLiquido.Should().Be(100m);
        conta.StatusContaId.Should().Be(StatusConta.PendenteId);
        conta.Rateios.Should().HaveCount(1);
    }

    [Fact]
    public void Criar_ComPagadorVazio_DeveLancarExcecao()
    {
        var act = () => ContaReceber.Criar(null, new DateOnly(2026, 4, 1), null, Guid.Empty,
            new DateOnly(2026, 4, 10), Guid.NewGuid(), null, null, 100m, 0, 0, 0, 1, 1, null,
            "Desc", null, StatusConta.PendenteId, false, null, OrigemLancamento.Manual,
            new[] { RateioPlano.Create(Guid.NewGuid(), 100m) });

        act.Should().Throw<ArgumentException>().WithMessage("*pagador*");
    }

    [Fact]
    public void Criar_ComFormaPagamentoVazia_DeveLancarExcecao()
    {
        var act = () => ContaReceber.Criar(null, new DateOnly(2026, 4, 1), null, Guid.NewGuid(),
            new DateOnly(2026, 4, 10), Guid.Empty, null, null, 100m, 0, 0, 0, 1, 1, null,
            "Desc", null, StatusConta.PendenteId, false, null, OrigemLancamento.Manual,
            new[] { RateioPlano.Create(Guid.NewGuid(), 100m) });

        act.Should().Throw<ArgumentException>().WithMessage("*orma de pagamento*");
    }

    [Fact]
    public void Cancelar_QuandoContaEstiverLiquidada_DeveFalhar()
    {
        var conta = CriarConta();
        conta.Liquidar(new DateOnly(2026, 4, 5), Guid.NewGuid(), StatusConta.LiquidadaId);

        var action = () => conta.Cancelar(StatusConta.CanceladaId);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancelar_ContaPendente_DeveDefinirStatusCancelada()
    {
        var conta = CriarConta();
        conta.Cancelar(StatusConta.CanceladaId);
        conta.StatusContaId.Should().Be(StatusConta.CanceladaId);
    }

    [Fact]
    public void Atualizar_QuandoContaEstiverLiquidada_DeveFalhar()
    {
        var conta = CriarConta();
        conta.Liquidar(new DateOnly(2026, 4, 5), Guid.NewGuid(), StatusConta.LiquidadaId);

        var action = () => conta.Atualizar(
            numeroDocumento: "REC-1",
            dataEmissao: new DateOnly(2026, 4, 4),
            responsavelId: null,
            pagadorId: conta.PagadorId,
            dataVencimento: new DateOnly(2026, 4, 12),
            formaPagamentoId: conta.FormaPagamentoId,
            cartaoId: null,
            contaBancariaId: null,
            valorOriginal: 80m,
            valorDesconto: 0m,
            valorJuros: 0m,
            valorMulta: 0m,
            descricao: "Recebimento",
            observacao: null,
            statusContaId: StatusConta.PendenteId,
            rateios: new[] { RateioPlano.Create(conta.Rateios.Single().ContaGerencialId, 80m) });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Não é permitido editar contas liquidadas ou canceladas.*");
    }

    [Fact]
    public void Liquidar_ContaCancelada_DeveLancarExcecao()
    {
        var conta = CriarConta();
        conta.Cancelar(StatusConta.CanceladaId);

        var act = () => conta.Liquidar(new DateOnly(2026, 4, 5), Guid.NewGuid(), StatusConta.LiquidadaId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cancelada*");
    }

    [Fact]
    public void Liquidar_ContaPendente_DeveDefinirDataLiquidacao()
    {
        var conta = CriarConta();
        var dataLiq = new DateOnly(2026, 4, 5);
        var contaBancariaId = Guid.NewGuid();

        conta.Liquidar(dataLiq, contaBancariaId, StatusConta.LiquidadaId);

        conta.DataLiquidacao.Should().Be(dataLiq);
        conta.StatusContaId.Should().Be(StatusConta.LiquidadaId);
        conta.ContaBancariaId.Should().Be(contaBancariaId);
    }

    [Fact]
    public void Estornar_ContaLiquidada_DeveVoltarParaPendente()
    {
        var conta = CriarConta();
        conta.Liquidar(new DateOnly(2026, 4, 5), Guid.NewGuid(), StatusConta.LiquidadaId);

        conta.Estornar(StatusConta.PendenteId);

        conta.StatusContaId.Should().Be(StatusConta.PendenteId);
        conta.DataLiquidacao.Should().BeNull();
        conta.ContaBancariaId.Should().BeNull();
    }

    [Fact]
    public void Estornar_ContaPendente_DeveLancarExcecao()
    {
        var conta = CriarConta();

        var act = () => conta.Estornar(StatusConta.PendenteId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*liquidadas*");
    }

    [Fact]
    public void VincularRecorrencia_ComIdValido_DeveDefinirRecorrencia()
    {
        var conta = CriarConta();
        var regraId = Guid.NewGuid();

        conta.VincularRecorrencia(regraId);

        conta.EhRecorrente.Should().BeTrue();
        conta.RegraRecorrenciaId.Should().Be(regraId);
    }

    [Fact]
    public void VincularRecorrencia_ContaLiquidada_DeveLancarExcecao()
    {
        var conta = CriarConta();
        conta.Liquidar(new DateOnly(2026, 4, 5), Guid.NewGuid(), StatusConta.LiquidadaId);

        var act = () => conta.VincularRecorrencia(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void VincularRecorrencia_ComIdVazio_DeveLancarExcecao()
    {
        var conta = CriarConta();

        var act = () => conta.VincularRecorrencia(Guid.Empty);

        act.Should().Throw<ArgumentException>().WithMessage("*ecorrência*");
    }

    [Fact]
    public void AtualizarValorLiquido_ValorValido_DeveAtualizarValores()
    {
        var conta = CriarConta(100m);
        var novoRateio = new[] { RateioPlano.Create(Guid.NewGuid(), 120m) };

        conta.AtualizarValorLiquido(120m, novoRateio);

        conta.ValorLiquido.Should().Be(120m);
    }

    [Fact]
    public void AtualizarValorLiquido_ContaLiquidada_DeveLancarExcecao()
    {
        var conta = CriarConta();
        conta.Liquidar(new DateOnly(2026, 4, 5), Guid.NewGuid(), StatusConta.LiquidadaId);

        var act = () => conta.AtualizarValorLiquido(90m, new[] { RateioPlano.Create(Guid.NewGuid(), 90m) });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AtualizarValorLiquido_ValorZero_DeveLancarExcecao()
    {
        var conta = CriarConta();

        var act = () => conta.AtualizarValorLiquido(0m, new[] { RateioPlano.Create(Guid.NewGuid(), 0m) });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void VincularContaContraria_IdValido_DeveDefinirVinculo()
    {
        var conta = CriarConta();
        var contaVinculadaId = Guid.NewGuid();

        conta.VincularContaContraria(contaVinculadaId, TipoContaVinculada.Pagar);

        conta.ContaVinculadaId.Should().Be(contaVinculadaId);
        conta.TipoContaVinculada.Should().Be(TipoContaVinculada.Pagar);
    }

    [Fact]
    public void VincularContaContraria_IdVazio_DeveLancarExcecao()
    {
        var conta = CriarConta();

        var act = () => conta.VincularContaContraria(Guid.Empty, TipoContaVinculada.Pagar);

        act.Should().Throw<ArgumentException>().WithMessage("*vinculada*");
    }

    [Fact]
    public void DesvincularContaContraria_DeveNulificarVinculo()
    {
        var conta = CriarConta();
        conta.VincularContaContraria(Guid.NewGuid(), TipoContaVinculada.Pagar);

        conta.DesvincularContaContraria();

        conta.ContaVinculadaId.Should().BeNull();
        conta.TipoContaVinculada.Should().BeNull();
    }

    [Fact]
    public void CriarParcelas_QuantidadeUm_RetornaUmaContaSemGrupo()
    {
        var rateios = new[] { RateioPlano.Create(Guid.NewGuid(), 300m) };

        var parcelas = ContaReceber.CriarParcelas(
            null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(),
            new DateOnly(2026, 1, 10), Guid.NewGuid(), null, null,
            300m, 0, 0, 0, 1, "Desc", null, StatusConta.PendenteId,
            false, null, OrigemLancamento.Manual, rateios);

        parcelas.Should().HaveCount(1);
        parcelas.First().GrupoParcelamentoId.Should().BeNull();
    }

    [Fact]
    public void CriarParcelas_TresParcelas_RetornaTresContasComMesmoGrupo()
    {
        var contaGerId = Guid.NewGuid();
        var rateios = new[] { RateioPlano.Create(contaGerId, 300m) };

        var parcelas = ContaReceber.CriarParcelas(
            null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(),
            new DateOnly(2026, 1, 10), Guid.NewGuid(), null, null,
            300m, 0, 0, 0, 3, "Desc", null, StatusConta.PendenteId,
            false, null, OrigemLancamento.Manual, rateios).ToList();

        parcelas.Should().HaveCount(3);
        parcelas.Select(p => p.GrupoParcelamentoId).Distinct().Should().HaveCount(1);
        parcelas.Select(p => p.NumeroParcela).Should().BeEquivalentTo([1, 2, 3]);
    }
}
