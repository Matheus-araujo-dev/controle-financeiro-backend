using ControleFinanceiro.Domain.Conciliacao;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Conciliacao;

public sealed class ConciliacaoFaturaTests
{
    private static ItemConciliacao Item(string key) => ItemConciliacao.CriarFatura(new DateOnly(2026, 9, 1), "COMPRA", 33.33m, key, 2, 3);

    [Fact]
    public void CriarFatura_ReaproveitaSessaoSemInventarContaBancaria()
    {
        var invoice = Guid.NewGuid();
        var session = Domain.Conciliacao.Conciliacao.CriarFatura("fatura.pdf", invoice, "hash", [Item("a")]);
        session.ContaBancariaId.Should().BeNull();
        session.FaturaId.Should().Be(invoice);
        session.Formato.Should().Be(FormatoArquivo.Pdf);
        session.Itens.Single().NumeroParcela.Should().Be(2);
    }

    [Fact]
    public void Vincular_AuditaDiferencaEPreservaOriginal_ERepeticaoEIdempotente()
    {
        var item = Item("a");
        var session = Domain.Conciliacao.Conciliacao.CriarFatura("fatura.pdf", Guid.NewGuid(), "hash", [item]);
        var account = Guid.NewGuid();
        session.ConciliarContaPagar(item.Id, account, 33.34m);
        session.ConciliarContaPagar(item.Id, account, 33.34m);
        item.Valor.Should().Be(33.33m);
        item.ValorAnteriorSistema.Should().Be(33.34m);
        item.ContaPagarVinculadaId.Should().Be(account);
        session.ItensConciliados.Should().Be(1);
        session.Status.Should().Be(StatusConciliacao.Concluida);
    }

    [Fact]
    public void DuasComprasIguais_NaoPodemConsumirMesmaConta()
    {
        var a = Item("a"); var b = Item("b");
        var session = Domain.Conciliacao.Conciliacao.CriarFatura("fatura.pdf", Guid.NewGuid(), "hash", [a,b]);
        var account = Guid.NewGuid();
        session.ConciliarContaPagar(a.Id, account, 33.33m);
        var act = () => session.ConciliarContaPagar(b.Id, account, 33.33m);
        act.Should().Throw<InvalidOperationException>();
        b.StatusItem.Should().Be(StatusItemConciliacao.Pendente);
    }

    [Fact]
    public void SessaoBancaria_NaoAceitaVinculoDeContaPagar()
    {
        var item = Item("a");
        var session = Domain.Conciliacao.Conciliacao.Criar("banco.csv", FormatoArquivo.Csv, Guid.NewGuid(), item.Data, item.Data, [item]);
        var act = () => session.ConciliarContaPagar(item.Id, Guid.NewGuid(), 33.33m);
        act.Should().Throw<InvalidOperationException>();
    }
}
