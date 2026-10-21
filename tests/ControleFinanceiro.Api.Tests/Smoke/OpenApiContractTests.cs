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
        if (!JsonNode.DeepEquals(actual, expected))
        {
            var diffs = FindDifferences(actual, expected, "$", 20);
            var diffText = string.Join("\n", diffs);
            true.Should().BeFalse($"DTO/endpoint changes must update the published contract and frontend types together.\nDifferences:\n{diffText}");
        }
    }

    private static List<string> FindDifferences(JsonNode? a, JsonNode? b, string path, int max)
    {
        var diffs = new List<string>();
        FindDiffsRecursive(a, b, path, diffs, max);
        return diffs;
    }

    private static void FindDiffsRecursive(JsonNode? a, JsonNode? b, string path, List<string> diffs, int max)
    {
        if (diffs.Count >= max) return;
        if (a is null && b is null) return;
        if (a is null) { diffs.Add($"{path}: actual=null, expected={Truncate(b)}"); return; }
        if (b is null) { diffs.Add($"{path}: actual={Truncate(a)}, expected=null"); return; }
        if (a is JsonObject ao && b is JsonObject bo)
        {
            foreach (var key in ao.Select(p => p.Key).Union(bo.Select(p => p.Key)).Distinct())
            {
                if (diffs.Count >= max) return;
                var av = ao.ContainsKey(key) ? ao[key] : null;
                var bv = bo.ContainsKey(key) ? bo[key] : null;
                if (av is null) { diffs.Add($"{path}.{key}: missing in actual"); continue; }
                if (bv is null) { diffs.Add($"{path}.{key}: extra in actual"); continue; }
                FindDiffsRecursive(av, bv, $"{path}.{key}", diffs, max);
            }
        }
        else if (a is JsonArray aa && b is JsonArray ba)
        {
            if (aa.Count != ba.Count) diffs.Add($"{path}: array length actual={aa.Count}, expected={ba.Count}");
            for (int i = 0; i < Math.Min(aa.Count, ba.Count); i++)
            {
                if (diffs.Count >= max) return;
                FindDiffsRecursive(aa[i], ba[i], $"{path}[{i}]", diffs, max);
            }
        }
        else if (!JsonNode.DeepEquals(a, b))
        {
            diffs.Add($"{path}: actual={Truncate(a)}, expected={Truncate(b)}");
        }
    }

    private static string Truncate(JsonNode? node)
    {
        if (node is null) return "null";
        var s = node.ToJsonString();
        return s.Length > 200 ? s[..200] + "..." : s;
    }
}