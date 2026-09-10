using System.Text.Json.Nodes;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Smoke;

public sealed class OpenApiContractTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task PublishedContract_MatchesRuntimeSwagger()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        var actual = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ControleFinanceiro.sln")))
            root = root.Parent;
        root.Should().NotBeNull("the test runs from the backend checkout");
        var snapshot = Path.Combine(root!.FullName, "contracts", "openapi.json");
        if (Environment.GetEnvironmentVariable("CF_UPDATE_OPENAPI") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshot)!);
            await File.WriteAllTextAsync(snapshot, actual.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        File.Exists(snapshot).Should().BeTrue("publish the reviewed API contract using CF_UPDATE_OPENAPI=1");
        var expected = JsonNode.Parse(await File.ReadAllTextAsync(snapshot));
        JsonNode.DeepEquals(actual, expected).Should().BeTrue("DTO/endpoint changes must update the published contract and frontend types together");
    }
}