using ControleFinanceiro.Application.Bootstrap;
using ControleFinanceiro.Contracts.Filters;
using FluentAssertions;

namespace ControleFinanceiro.Application.Tests.Bootstrap;

public sealed class BootstrapCatalogServiceTests
{
    private readonly BootstrapCatalogService _service = new();

    [Fact]
    public void ListModules_ShouldFilterBySearchAndRespectPaging()
    {
        var query = new ListQueryRequest
        {
            Search = "contas",
            Page = 1,
            PageSize = 2
        };

        var result = _service.ListModules(query);

        result.TotalItems.Should().Be(4);
        result.TotalPages.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Select(item => item.Route).Should().OnlyContain(route => route.Contains("contas", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ListModules_ShouldReturnAllModulesWhenSearchIsEmpty()
    {
        var result = _service.ListModules(new ListQueryRequest());

        result.TotalItems.Should().Be(12);
        result.Items.Should().NotBeEmpty();
    }
}
