using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;
using Xunit;

namespace ControleFinanceiro.Domain.Tests.Financeiro;

public class TransferenciaTests
{
    private static readonly Guid Origem = Guid.NewGuid();
    private static readonly Guid Destino = Guid.NewGuid();

    [Fact]
    public void Criar_ComDadosValidos_DeveCriarTransferencia()
    {
        var data = new DateOnly(2026, 7, 9);
        var t = Transferencia.Criar(Origem, Destino, 500m, data, "Reserva");

        t.ContaBancariaOrigemId.Should().Be(Origem);
        t.ContaBancariaDestinoId.Should().Be(Destino);
        t.Valor.Should().Be(500m);
        t.DataTransferencia.Should().Be(data);
        t.Descricao.Should().Be("Reserva");
        t.Cancelada.Should().BeFalse();
        t.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Criar_ComDescricaoComEspacos_DeveTrimmar()
    {
        var t = Transferencia.Criar(Origem, Destino, 100m, DateOnly.FromDateTime(DateTime.Today), "  PIX  ");
        t.Descricao.Should().Be("PIX");
    }

    [Fact]
    public void Criar_ComDescricaoVazia_DeveArmazenarNull()
    {
        var t = Transferencia.Criar(Origem, Destino, 100m, DateOnly.FromDateTime(DateTime.Today), "   ");
        t.Descricao.Should().BeNull();
    }

    [Fact]
    public void Criar_ComOrigemIgualDestino_DeveLancarExcecao()
    {
        var acao = () => Transferencia.Criar(Origem, Origem, 100m, DateOnly.FromDateTime(DateTime.Today), null);
        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Criar_ComValorZero_DeveLancarExcecao()
    {
        var acao = () => Transferencia.Criar(Origem, Destino, 0m, DateOnly.FromDateTime(DateTime.Today), null);
        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Criar_ComValorNegativo_DeveLancarExcecao()
    {
        var acao = () => Transferencia.Criar(Origem, Destino, -50m, DateOnly.FromDateTime(DateTime.Today), null);
        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancelar_QuandoPendente_DeveCancelar()
    {
        var t = Transferencia.Criar(Origem, Destino, 100m, DateOnly.FromDateTime(DateTime.Today), null);
        t.Cancelar();
        t.Cancelada.Should().BeTrue();
    }

    [Fact]
    public void Cancelar_QuandoJaCancelada_DeveLancarExcecao()
    {
        var t = Transferencia.Criar(Origem, Destino, 100m, DateOnly.FromDateTime(DateTime.Today), null);
        t.Cancelar();
        var acao = () => t.Cancelar();
        acao.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MovimentacaoFinanceira_CriarSaidaTransferencia_DeveSerSaida()
    {
        var transferenciaId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.Today);
        var mov = MovimentacaoFinanceira.CriarSaidaTransferencia(transferenciaId, Origem, data, 200m, "Transf");

        mov.Tipo.Should().Be(TipoMovimentacao.Saida);
        mov.Natureza.Should().Be(NaturezaMovimentacao.Realizada);
        mov.ContaBancariaId.Should().Be(Origem);
        mov.TransferenciaId.Should().Be(transferenciaId);
        mov.Valor.Should().Be(200m);
        mov.StatusMovimentacaoId.Should().Be(StatusMovimentacao.EfetivadaId);
    }

    [Fact]
    public void MovimentacaoFinanceira_CriarEntradaTransferencia_DeveSerEntrada()
    {
        var transferenciaId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.Today);
        var mov = MovimentacaoFinanceira.CriarEntradaTransferencia(transferenciaId, Destino, data, 200m, "Transf");

        mov.Tipo.Should().Be(TipoMovimentacao.Entrada);
        mov.Natureza.Should().Be(NaturezaMovimentacao.Realizada);
        mov.ContaBancariaId.Should().Be(Destino);
        mov.TransferenciaId.Should().Be(transferenciaId);
        mov.Valor.Should().Be(200m);
    }
}
