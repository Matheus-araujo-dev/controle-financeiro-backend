using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Contracts.Errors;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Bootstrap;

public sealed class BootstrapControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetModules_ShouldReturnPagedModules()
    {
        var response = await _client.GetAsync("/api/v1/bootstrap/modules?search=contas&page=1&pageSize=2");
        var payload = await response.Content.ReadFromJsonAsync<BootstrapModulesResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.Should().NotBeNull();
        payload!.TotalItems.Should().Be(4);
        payload.Page.Should().Be(1);
        payload.PageSize.Should().Be(2);
        payload.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task PostEcho_WhenRequestIsInvalid_ShouldReturnValidationContract()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/bootstrap/echo", new { });
        var payload = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        payload.Should().NotBeNull();
        payload!.Code.Should().Be("VALIDATION_ERROR");
        payload.Errors.Should().ContainKey("Name");
    }

    private sealed record BootstrapModulesResponse(
        IReadOnlyCollection<BootstrapModuleItem> Items,
        int Page,
        int PageSize,
        int TotalItems,
        int TotalPages);

    private sealed record BootstrapModuleItem(string Code, string Name, string Route, int Phase);
}
