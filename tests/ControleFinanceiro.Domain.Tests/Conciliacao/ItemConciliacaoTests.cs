using ControleFinanceiro.Domain.Conciliacao;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Conciliacao;

public sealed class ItemConciliacaoTests
{
    [Fact]
    public void CriarFatura_ComDadosValidos_CriaItemCorreto()
    {
        var item = ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra Teste", 100.50m, "chave|123", 2, 5);

        item.Data.Should().Be(new DateOnly(2026, 1, 15));
        item.Descricao.Should().Be("Compra Teste");
        item.Valor.Should().Be(100.50m);
        item.NumeroParcela.Should().Be(2);
        item.QuantidadeParcelas.Should().Be(5);
        item.StatusItem.Should().Be(StatusItemConciliacao.Pendente);
    }

    [Fact]
    public void CriarFatura_ChaveVazia_LancaExcecao()
    {
        var act = () => ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra", 100m, "", 1, 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CriarFatura_DescricaoVazia_LancaExcecao()
    {
        var act = () => ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "  ", 100m, "chave", 1, 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CriarFatura_ValorZero_LancaExcecao()
    {
        var act = () => ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra", 0m, "chave", 1, 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CriarFatura_ValorComMaisDeDuasCasas_LancaExcecao()
    {
        var act = () => ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra", 10.123m, "chave", 1, 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CriarFatura_NumeroParcelaZero_LancaExcecao()
    {
        var act = () => ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra", 100m, "chave", 0, 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CriarFatura_NumeroParcelaMaiorQueTotal_LancaExcecao()
    {
        var act = () => ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra", 100m, "chave", 3, 2);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CriarFatura_MaisDe120Parcelas_LancaExcecao()
    {
        var act = () => ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra", 100m, "chave", 1, 121);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CriarFatura_ValorNegativo_CriaItemCredito()
    {
        var item = ItemConciliacao.CriarFatura(new DateOnly(2026, 3, 10), "Estorno", -50.00m, "chave|est", 1, 1);
        item.Valor.Should().Be(-50.00m);
        item.StatusItem.Should().Be(StatusItemConciliacao.Pendente);
    }

    [Fact]
    public void SalvarRascunho_ItemPendente_SalvaJson()
    {
        var item = ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra", 100m, "chave", 1, 1);
        item.SalvarRascunho("{\"campo\":\"valor\"}");
        item.RascunhoJson.Should().Be("{\"campo\":\"valor\"}");
    }

    [Fact]
    public void SalvarRascunho_JsonVazio_LancaExcecao()
    {
        var item = ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra", 100m, "chave", 1, 1);
        var act = () => item.SalvarRascunho("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SalvarRascunho_ItemJaConciliado_LancaExcecao()
    {
        var item = ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra", 100m, "chave", 1, 1);
        item.ConciliarContaPagar(Guid.NewGuid(), null);
        var act = () => item.SalvarRascunho("{}");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ConciliarContaPagar_ItemPendente_Concilia()
    {
        var item = ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra", 100m, "chave", 1, 1);
        var contaId = Guid.NewGuid();
        item.ConciliarContaPagar(contaId, 95.50m);

        item.ContaPagarVinculadaId.Should().Be(contaId);
        item.ValorAnteriorSistema.Should().Be(95.50m);
        item.StatusItem.Should().Be(StatusItemConciliacao.Conciliado);
    }

    [Fact]
    public void ConciliarContaPagar_GuidEmpty_LancaExcecao()
    {
        var item = ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra", 100m, "chave", 1, 1);
        var act = () => item.ConciliarContaPagar(Guid.Empty, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConciliarContaPagar_SemChaveOrigem_LancaExcecao()
    {
        var item = ItemConciliacao.Criar(new DateOnly(2026, 1, 15), "Movimentação", 100m, null);
        var act = () => item.ConciliarContaPagar(Guid.NewGuid(), null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConciliarContaPagar_IdempotenteMesmaConta_NaoLancaExcecao()
    {
        var item = ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra", 100m, "chave", 1, 1);
        var contaId = Guid.NewGuid();
        item.ConciliarContaPagar(contaId, null);
        item.ConciliarContaPagar(contaId, null);
        item.StatusItem.Should().Be(StatusItemConciliacao.Conciliado);
    }

    [Fact]
    public void ConciliarContaPagar_ItemIgnorado_LancaExcecao()
    {
        var item = ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra", 100m, "chave", 1, 1);
        item.Ignorar();
        var act = () => item.ConciliarContaPagar(Guid.NewGuid(), null);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Ignorar_ItemPendente_MudaStatus()
    {
        var item = ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra", 100m, "chave", 1, 1);
        item.Ignorar();
        item.StatusItem.Should().Be(StatusItemConciliacao.Ignorado);
    }

    [Fact]
    public void Ignorar_ItemJaVinculado_LancaExcecao()
    {
        var item = ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra", 100m, "chave", 1, 1);
        item.ConciliarContaPagar(Guid.NewGuid(), null);
        var act = () => item.Ignorar();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DefinirSugestao_AtribuiValores()
    {
        var item = ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra", 100m, "chave", 1, 1);
        var movId = Guid.NewGuid();
        item.DefinirSugestao(movId, 0.85m);

        item.SugestaoMovimentacaoId.Should().Be(movId);
        item.ScoreSugestao.Should().Be(0.85m);
    }

    [Fact]
    public void Conciliar_AtribuiMovimentacaoEConcilia()
    {
        var item = ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra", 100m, "chave", 1, 1);
        var movId = Guid.NewGuid();
        item.Conciliar(movId);

        item.MovimentacaoVinculadaId.Should().Be(movId);
        item.StatusItem.Should().Be(StatusItemConciliacao.Conciliado);
    }

    [Fact]
    public void Conciliar_SemMovimentacao_ConciliaComNull()
    {
        var item = ItemConciliacao.CriarFatura(new DateOnly(2026, 1, 15), "Compra", 100m, "chave", 1, 1);
        item.Conciliar(null);

        item.MovimentacaoVinculadaId.Should().BeNull();
        item.StatusItem.Should().Be(StatusItemConciliacao.Conciliado);
    }

    [Fact]
    public void Criar_ComDocumento_TrimaEspacos()
    {
        var item = ItemConciliacao.Criar(new DateOnly(2026, 2, 20), "  Descrição  ", 50m, "  DOC123  ");
        item.Descricao.Should().Be("Descrição");
        item.Documento.Should().Be("DOC123");
        item.StatusItem.Should().Be(StatusItemConciliacao.Pendente);
    }

    [Fact]
    public void Criar_SemDocumento_DocumentoNull()
    {
        var item = ItemConciliacao.Criar(new DateOnly(2026, 2, 20), "Descrição", 50m, null);
        item.Documento.Should().BeNull();
    }
}
