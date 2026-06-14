using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ControleFinanceiro.SharedKernel.Common;

public class AuditTrailEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string? BeforeValues { get; set; }
    public string? AfterValues { get; set; }
    public string? PropertyName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

public static class AuditTrailSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, Options);
    }

    public static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;
        
        return JsonSerializer.Deserialize<T>(json, Options);
    }

    private static readonly HashSet<string> AlwaysSensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Senha",
        "Password",
        "Token",
        "RefreshToken",
        "RefreshTokenHash",
        "Chave",
        "ChavePix",
        "Secret",
        "ApiKey",
        "NumeroConta",
        "Agencia"
    };

    private static readonly Dictionary<string, HashSet<string>> SensitivePropertiesByEntity = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pessoa"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Nome",
            "Cpf",
            "Cnpj",
            "CpfCnpj",
            "Email",
            "Telefone",
            "Observacao"
        },
        ["PessoaChavePix"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Chave"
        },
        ["ContaBancaria"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Agencia",
            "NumeroConta",
            "SaldoInicial"
        },
        ["Cartao"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "LimiteCredito"
        }
    };

    public static string Sanitize(string? entityType, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json!;

        var node = JsonNode.Parse(json) as JsonObject;
        if (node is null)
            return json;

        var entityProperties = !string.IsNullOrWhiteSpace(entityType)
            && SensitivePropertiesByEntity.TryGetValue(entityType, out var configuredProperties)
                ? configuredProperties
                : null;

        foreach (var property in node.ToList())
        {
            if (AlwaysSensitiveProperties.Contains(property.Key)
                || entityProperties?.Contains(property.Key) == true)
            {
                node[property.Key] = "[REDACTED]";
            }
        }

        return node.ToJsonString(Options);
    }
}
