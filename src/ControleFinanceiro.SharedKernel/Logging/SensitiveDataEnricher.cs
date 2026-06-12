using Serilog.Core;
using Serilog.Events;

namespace ControleFinanceiro.SharedKernel.Logging;

public class SensitiveDataEnricher : ILogEventEnricher
{
    public const string MaskedValue = "***REDACTED***";

    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "PasswordHash",
        "ConnectionString",
        "Token",
        "Secret",
        "Key",
        "ApiKey",
        "ClientSecret",
        "AccessToken",
        "RefreshToken",
        "Authorization",
        "Credential",
        "PrivateKey",
        "PublicKey",
        "Signature",
        "Salt"
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var key in logEvent.Properties.Keys.ToList())
        {
            if (SensitivePropertyNames.Contains(key) && logEvent.Properties.TryGetValue(key, out var originalValue))
            {
                if (originalValue is ScalarValue scalar && scalar.Value != null)
                {
                    var redactedProperty = propertyFactory.CreateProperty(key, MaskedValue);
                    logEvent.AddOrUpdateProperty(redactedProperty);
                }
            }
        }
    }
}