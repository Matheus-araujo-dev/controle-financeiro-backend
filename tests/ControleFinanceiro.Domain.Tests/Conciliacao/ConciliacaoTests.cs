using ControleFinanceiro.Domain.Conciliacao;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Conciliacao;

public sealed class ConciliacaoTests
{
    private static List<ItemConciliacao> CriarItens(int qtd = 3) =>
        Enumerable.Range(1, qtd)
            .Select(i => ItemConciliacao.Criar(
                new DateOnly(2026, 1, i),
                $"Transacao {i}",
                100m * i,
                $"DOC{i}"))
            .ToList();

    [Fact]
    public void Criar_QuandoPayloadValido_DeveCriarConciliacaoEmRevisao()
    {
        var itens = CriarItens();
        var conciliacao = Domain.Conciliacao.Conciliacao.Criar(
            "extrato.ofx", FormatoArquivo.Ofx, Guid.NewGuid(),
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), itens);

        conciliacao.NomeArquivo.Should().Be("extrato.ofx");
        conciliacao.Formato.Should().Be(FormatoArquivo.Ofx);
        conciliacao.TotalItens.Should().Be(3);
        conciliacao.ItensConciliados.Should().Be(0);
        conciliacao.Status.Should().Be(StatusConciliacao.EmRevisao);
        conciliacao.Itens.Should().HaveCount(3);
    }

    [Fact]
    public void Criar_QuandoNomeArquivoVazio_DeveFalhar()
    {
        var action = () => Domain.Conciliacao.Conciliacao.Criar(
            "", FormatoArquivo.Ofx, Guid.NewGuid(),
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), CriarItens());

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Criar_QuandoContaBancariaVazia_DeveFalhar()
    {
        var action = () => Domain.Conciliacao.Conciliacao.Criar(
            "extrato.ofx", FormatoArquivo.Ofx, Guid.Empty,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), CriarItens());

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Criar_QuandoListaItensVazia_DeveFalhar()
    {
        var action = () => Domain.Conciliacao.Conciliacao.Criar(
            "extrato.ofx", FormatoArquivo.Ofx, Guid.NewGuid(),
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), []);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConciliarItem_DeveAtualizarContadorEStatus()
    {
        var itens = CriarItens(2);
        var conciliacao = Domain.Conciliacao.Conciliacao.Criar(
            "extrato.ofx", FormatoArquivo.Ofx, Guid.NewGuid(),
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), itens);

        var movId = Guid.NewGuid();
        conciliacao.ConciliarItem(itens[0].Id, movId);

        conciliacao.ItensConciliados.Should().Be(1);
        conciliacao.Status.Should().Be(StatusConciliacao.EmRevisao);
        itens[0].StatusItem.Should().Be(StatusItemConciliacao.Conciliado);
        itens[0].MovimentacaoVinculadaId.Should().Be(movId);
    }

    [Fact]
    public void ConciliarTodosItens_DeveFinalizarConciliacao()
    {
        var itens = CriarItens(2);
        var conciliacao = Domain.Conciliacao.Conciliacao.Criar(
            "extrato.ofx", FormatoArquivo.Ofx, Guid.NewGuid(),
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), itens);

        conciliacao.ConciliarItem(itens[0].Id, Guid.NewGuid());
        conciliacao.IgnorarItem(itens[1].Id);

        conciliacao.ItensConciliados.Should().Be(2);
        conciliacao.Status.Should().Be(StatusConciliacao.Concluida);
    }

    [Fact]
    public void IgnorarItem_DeveMarcarComoIgnorado()
    {
        var itens = CriarItens(1);
        var conciliacao = Domain.Conciliacao.Conciliacao.Criar(
            "extrato.ofx", FormatoArquivo.Ofx, Guid.NewGuid(),
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), itens);

        conciliacao.IgnorarItem(itens[0].Id);

        itens[0].StatusItem.Should().Be(StatusItemConciliacao.Ignorado);
        conciliacao.Status.Should().Be(StatusConciliacao.Concluida);
    }

    [Fact]
    public void ConciliarItem_QuandoItemNaoExiste_DeveFalhar()
    {
        var conciliacao = Domain.Conciliacao.Conciliacao.Criar(
            "extrato.ofx", FormatoArquivo.Ofx, Guid.NewGuid(),
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), CriarItens(1));

        var action = () => conciliacao.ConciliarItem(Guid.NewGuid(), Guid.NewGuid());

        action.Should().Throw<InvalidOperationException>();
    }
}