using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.SharedKernel.Common;
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
    public void ReatribuirFaturaCartao_DeveAtualizarOuNulificarVinculo()
    {
        var faturaId = Guid.NewGuid();
        var contaPagar = ContaPagar.Criar(
            numeroDocumento: null,
            dataEmissao: new DateOnly(2026, 4, 5),
            responsavelCompraId: null,
            recebedorId: Guid.NewGuid(),
            dataVencimento: new DateOnly(2026, 4, 20),
            formaPagamentoId: Guid.NewGuid(),
            cartaoId: Guid.NewGuid(),
            contaBancariaId: null,
            valorOriginal: 100m,
            valorDesconto: 0m,
            valorJuros: 0m,
            valorMulta: 0m,
            quantidadeParcelas: 1,
            numeroParcela: 1,
            grupoParcelamentoId: null,
            origemCompraPlanejadaId: null,
            descricao: "Compra teste",
            observacao: null,
            statusContaId: StatusConta.EmFaturaId,
            ehRecorrente: false,
            regraRecorrenciaId: null,
            origem: OrigemLancamento.Manual,
            rateios: [new RateioPlano(Guid.NewGuid(), 100m)]);

        contaPagar.VincularFaturaCartao(faturaId);
        contaPagar.FaturaCartaoId.Should().Be(faturaId);

        var novaFaturaId = Guid.NewGuid();
        contaPagar.ReatribuirFaturaCartao(novaFaturaId);
        contaPagar.FaturaCartaoId.Should().Be(novaFaturaId);

        contaPagar.ReatribuirFaturaCartao(null);
        contaPagar.FaturaCartaoId.Should().BeNull();
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

    [Fact]
    public void Fechar_DeveAlterarStatusParaFechada()
    {
        var fatura = FaturaCartao.Criar(
            cartaoId: Guid.NewGuid(),
            competencia: "2026-08",
            dataFechamento: new DateOnly(2026, 8, 10),
            dataVencimento: new DateOnly(2026, 8, 20),
            valorTotal: 200m,
            observacao: null);

        fatura.Fechar();

        fatura.Status.Should().Be(StatusFaturaCartao.Fechada);
    }

    [Fact]
    public void Fechar_QuandoJaFechada_DeveFalhar()
    {
        var fatura = FaturaCartao.Criar(
            cartaoId: Guid.NewGuid(),
            competencia: "2026-08",
            dataFechamento: new DateOnly(2026, 8, 10),
            dataVencimento: new DateOnly(2026, 8, 20),
            valorTotal: 200m,
            observacao: null);

        fatura.Fechar();
        var action = () => fatura.Fechar();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ja esta fechada*");
    }

    [Fact]
    public void Reabrir_QuandoFechada_DeveAlterarStatusParaAberta()
    {
        var fatura = FaturaCartao.Criar(
            cartaoId: Guid.NewGuid(),
            competencia: "2026-08",
            dataFechamento: new DateOnly(2026, 8, 10),
            dataVencimento: new DateOnly(2026, 8, 20),
            valorTotal: 200m,
            observacao: null);

        fatura.Fechar();
        fatura.Reabrir();

        fatura.Status.Should().Be(StatusFaturaCartao.Aberta);
    }

    [Fact]
    public void Reabrir_QuandoNaoFechada_DeveFalhar()
    {
        var fatura = FaturaCartao.Criar(
            cartaoId: Guid.NewGuid(),
            competencia: "2026-08",
            dataFechamento: new DateOnly(2026, 8, 10),
            dataVencimento: new DateOnly(2026, 8, 20),
            valorTotal: 200m,
            observacao: null);

        var action = () => fatura.Reabrir();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*fechadas*");
    }
}
