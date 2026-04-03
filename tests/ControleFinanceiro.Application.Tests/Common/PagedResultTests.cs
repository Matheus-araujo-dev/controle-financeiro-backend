using ControleFinanceiro.Contracts.Common;
using FluentAssertions;

namespace ControleFinanceiro.Application.Tests.Common;

public sealed class PagedResultTests
{
    [Fact]
    public void Create_ShouldCalculateTotalPagesUsingCeiling()
    {
        var result = PagedResult<int>.Create([1, 2], page: 1, pageSize: 2, totalItems: 5);

        result.TotalPages.Should().Be(3);
        result.TotalItems.Should().Be(5);
        result.Items.Should().ContainInOrder(1, 2);
    }

    [Fact]
    public void Create_ShouldNormalizeInvalidPagingArguments()
    {
        var result = PagedResult<int>.Create([], page: 0, pageSize: 0, totalItems: -1);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.TotalItems.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }
}
