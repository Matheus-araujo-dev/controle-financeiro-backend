using ControleFinanceiro.Application.Common.Extensions;
using FluentAssertions;

namespace ControleFinanceiro.Application.Tests.Common.Extensions;

public sealed class QueryableExtensionsTests
{
    private sealed record Item(int Id);

    private static IQueryable<Item> Itens(int quantidade) =>
        Enumerable.Range(1, quantidade).Select(i => new Item(i)).AsQueryable();

    [Fact]
    public void WhereIn_SemValores_DeveRetornarConsultaOriginal()
    {
        var resultado = Itens(5).WhereIn(x => x.Id, Array.Empty<int>());

        resultado.Should().HaveCount(5);
    }

    [Fact]
    public void WhereIn_ListaPequena_DeveFiltrarPorContains()
    {
        var resultado = Itens(10).WhereIn(x => x.Id, [2, 4, 6]).ToList();

        resultado.Select(x => x.Id).Should().BeEquivalentTo([2, 4, 6]);
    }

    [Fact]
    public void WhereIn_ListaGrande_DeveFiltrarPorJoin()
    {
        // > 50 valores aciona o caminho de Join (otimização para listas grandes).
        var valores = Enumerable.Range(1, 60).ToList();

        var resultado = Itens(100).WhereIn(x => x.Id, valores).ToList();

        resultado.Should().HaveCount(60);
        resultado.Select(x => x.Id).Should().BeEquivalentTo(valores);
    }
}
