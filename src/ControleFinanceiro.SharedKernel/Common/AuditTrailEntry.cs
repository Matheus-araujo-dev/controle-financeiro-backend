using System.Text.Json;
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

    private static readonly Dictionary<string, List<string>> SensitiveProperties = new()
    {
        { "Pessoa", ["Cpf", "Cnpj", "Nome", "Email", "Telefone", "ChavePix"] },
        { "ContaBancaria", ["Saldo"] },
        { "Cartao", ["LimiteCredito"] }
    };

    public static string Sanitize(string? entityType, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json!;

        if (string.IsNullOrWhiteSpace(entityType) || !SensitiveProperties.TryGetValue(entityType, out var props))
            return json;

        var result = json;
        foreach (var prop in props)
        {
            var pattern = $"\"{prop}\":\"[^\"]*\"";
            result = Regex.Replace(result, pattern, $"\"{prop}\":\"[REDACTED]\"", RegexOptions.IgnoreCase);
            
            var numberPattern = $"\"{prop}\":\\s*\\d+(\\.\\d+)?";
            result = Regex.Replace(result, numberPattern, $"\"{prop}\":[REDACTED]", RegexOptions.IgnoreCase);
        }

        return result;
    }
}