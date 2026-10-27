using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public sealed class MovimentacaoFinanceiraTests
{
    [Fact]
    public void CriarSaidaTransferencia_ComDadosValidos_CriaMovimentacao()
    {
        var transferenciaId = Guid.NewGuid();
        var contaOrigemId = Guid.NewGuid();
        var mov = MovimentacaoFinanceira.CriarSaidaTransferencia(
            transferenciaId, contaOrigemId, new DateOnly(2026, 3, 15), 250.50m, "Transferência PIX");

        mov.TransferenciaId.Should().Be(transferenciaId);
        mov.ContaBancariaId.Should().Be(contaOrigemId);
        mov.Tipo.Should().Be(TipoMovimentacao.Saida);
        mov.Natureza.Should().Be(NaturezaMovimentacao.Realizada);
        mov.Valor.Should().Be(250.50m);
        mov.StatusMovimentacaoId.Should().Be(StatusMovimentacao.EfetivadaId);
        mov.Observacao.Should().Be("Transferência PIX");
    }

    [Fact]
    public void CriarSaidaTransferencia_ValorZero_LancaExcecao()
    {
        var act = () => MovimentacaoFinanceira.CriarSaidaTransferencia(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), 0m, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CriarSaidaTransferencia_ValorNegativo_LancaExcecao()
    {
        var act = () => MovimentacaoFinanceira.CriarSaidaTransferencia(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), -10m, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CriarSaidaTransferencia_ObservacaoNula_DefineNull()
    {
        var mov = MovimentacaoFinanceira.CriarSaidaTransferencia(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), 100m, null);
        mov.Observacao.Should().BeNull();
    }

    [Fact]
    public void CriarSaidaTransferencia_ObservacaoVazia_DefineNull()
    {
        var mov = MovimentacaoFinanceira.CriarSaidaTransferencia(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), 100m, "   ");
        mov.Observacao.Should().BeNull();
    }

    [Fact]
    public void CriarSaidaTransferencia_ObservacaoComEspacos_Trima()
    {
        var mov = MovimentacaoFinanceira.CriarSaidaTransferencia(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), 100m, "  obs  ");
        mov.Observacao.Should().Be("obs");
    }

    [Fact]
    public void CriarSaidaTransferencia_ValorComCasasExtras_Arredonda()
    {
        var mov = MovimentacaoFinanceira.CriarSaidaTransferencia(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), 100.125m, null);
        mov.Valor.Should().Be(100.13m);
    }

    [Fact]
    public void CriarEntradaTransferencia_ComDadosValidos_CriaMovimentacao()
    {
        var transferenciaId = Guid.NewGuid();
        var contaDestinoId = Guid.NewGuid();
        var mov = MovimentacaoFinanceira.CriarEntradaTransferencia(
            transferenciaId, contaDestinoId, new DateOnly(2026, 5, 20), 500m, "Recebido");

        mov.TransferenciaId.Should().Be(transferenciaId);
        mov.ContaBancariaId.Should().Be(contaDestinoId);
        mov.Tipo.Should().Be(TipoMovimentacao.Entrada);
        mov.Natureza.Should().Be(NaturezaMovimentacao.Realizada);
        mov.Valor.Should().Be(500m);
        mov.Observacao.Should().Be("Recebido");
    }

    [Fact]
    public void CriarEntradaTransferencia_ValorZero_LancaExcecao()
    {
        var act = () => MovimentacaoFinanceira.CriarEntradaTransferencia(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), 0m, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CriarLiquidacaoContaPagar_ComDadosValidos_CriaMovimentacao()
    {
        var contaPagarId = Guid.NewGuid();
        var contaBancariaId = Guid.NewGuid();
        var mov = MovimentacaoFinanceira.CriarLiquidacaoContaPagar(
            contaPagarId, contaBancariaId, new DateOnly(2026, 4, 10), 150m, StatusMovimentacao.EfetivadaId, "Pagamento");

        mov.ContaPagarId.Should().Be(contaPagarId);
        mov.ContaBancariaId.Should().Be(contaBancariaId);
        mov.Tipo.Should().Be(TipoMovimentacao.Saida);
        mov.Natureza.Should().Be(NaturezaMovimentacao.Realizada);
        mov.Valor.Should().Be(150m);
    }

    [Fact]
    public void CriarLiquidacaoContaPagar_ValorZero_LancaExcecao()
    {
        var act = () => MovimentacaoFinanceira.CriarLiquidacaoContaPagar(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), 0m, StatusMovimentacao.EfetivadaId, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CriarLiquidacaoContaPagar_ComFaturaCartao_DefineFaturaId()
    {
        var faturaId = Guid.NewGuid();
        var mov = MovimentacaoFinanceira.CriarLiquidacaoContaPagar(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 4, 10), 150m, StatusMovimentacao.EfetivadaId, null, faturaId);

        mov.FaturaCartaoId.Should().Be(faturaId);
    }

    [Fact]
    public void CriarCompraCartaoEconomica_ContaBancariaNula()
    {
        var mov = MovimentacaoFinanceira.CriarCompraCartaoEconomica(
            Guid.NewGuid(), new DateOnly(2026, 6, 1), 200m, StatusMovimentacao.PrevistaId, "Compra cartão");

        mov.ContaBancariaId.Should().BeNull();
        mov.Natureza.Should().Be(NaturezaMovimentacao.Economica);
        mov.Tipo.Should().Be(TipoMovimentacao.Saida);
    }

    [Fact]
    public void CriarLiquidacaoContaReceber_ComDadosValidos_CriaMovimentacao()
    {
        var contaReceberId = Guid.NewGuid();
        var mov = MovimentacaoFinanceira.CriarLiquidacaoContaReceber(
            contaReceberId, Guid.NewGuid(), new DateOnly(2026, 7, 15), 800m, StatusMovimentacao.EfetivadaId, "Recebimento");

        mov.ContaReceberId.Should().Be(contaReceberId);
        mov.Tipo.Should().Be(TipoMovimentacao.Entrada);
        mov.Natureza.Should().Be(NaturezaMovimentacao.Realizada);
    }

    [Fact]
    public void CriarPagamentoFatura_ComDadosValidos_CriaMovimentacao()
    {
        var faturaId = Guid.NewGuid();
        var mov = MovimentacaoFinanceira.CriarPagamentoFatura(
            faturaId, Guid.NewGuid(), new DateOnly(2026, 8, 5), 1200m, StatusMovimentacao.EfetivadaId, null);

        mov.FaturaCartaoId.Should().Be(faturaId);
        mov.ContaPagarId.Should().BeNull();
        mov.ContaReceberId.Should().BeNull();
        mov.Tipo.Should().Be(TipoMovimentacao.Saida);
    }

    [Fact]
    public void AtualizarEconomicaContaPagar_ComDadosValidos_Atualiza()
    {
        var mov = MovimentacaoFinanceira.CriarCompraCartaoEconomica(
            Guid.NewGuid(), new DateOnly(2026, 1, 1), 50m, StatusMovimentacao.PrevistaId, "original");

        mov.AtualizarEconomicaContaPagar(new DateOnly(2026, 2, 1), 75m, StatusMovimentacao.EfetivadaId, "atualizado");

        mov.DataMovimentacao.Should().Be(new DateOnly(2026, 2, 1));
        mov.Valor.Should().Be(75m);
        mov.StatusMovimentacaoId.Should().Be(StatusMovimentacao.EfetivadaId);
        mov.Observacao.Should().Be("atualizado");
    }

    [Fact]
    public void AtualizarEconomicaContaPagar_MovimentacaoNaoEconomica_LancaExcecao()
    {
        var mov = MovimentacaoFinanceira.CriarSaidaTransferencia(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), 100m, null);

        var act = () => mov.AtualizarEconomicaContaPagar(new DateOnly(2026, 2, 1), 75m, StatusMovimentacao.EfetivadaId, null);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AtualizarEconomicaContaPagar_ValorZero_LancaExcecao()
    {
        var mov = MovimentacaoFinanceira.CriarCompraCartaoEconomica(
            Guid.NewGuid(), new DateOnly(2026, 1, 1), 50m, StatusMovimentacao.PrevistaId, null);

        var act = () => mov.AtualizarEconomicaContaPagar(new DateOnly(2026, 2, 1), 0m, StatusMovimentacao.EfetivadaId, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancelar_AlteraStatusMovimentacao()
    {
        var mov = MovimentacaoFinanceira.CriarCompraCartaoEconomica(
            Guid.NewGuid(), new DateOnly(2026, 1, 1), 50m, StatusMovimentacao.PrevistaId, null);

        mov.Cancelar(StatusMovimentacao.CanceladaId);

        mov.StatusMovimentacaoId.Should().Be(StatusMovimentacao.CanceladaId);
    }
}
