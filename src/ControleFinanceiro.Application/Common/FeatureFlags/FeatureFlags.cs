using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Application.Common.FeatureFlags;

public interface IFeatureFlagService
{
    bool IsEnabled(string featureName);
    string GetVariant(string featureName, string defaultVariant = "control");
    Task<bool> EvaluateAsync(string featureName, bool defaultValue = false);
}

public sealed class FeatureFlagsOptions
{
    public const string SectionName = "FeatureFlags";
    
    public Dictionary<string, FeatureFlagConfig> Flags { get; set; } = new();
}

public class FeatureFlagConfig
{
    public bool Enabled { get; set; }
    public string Variant { get; set; } = "control";
    public double RolloutPercentage { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public sealed class InMemoryFeatureFlagService : IFeatureFlagService
{
    private readonly FeatureFlagsOptions _options;
    private readonly ILogger<InMemoryFeatureFlagService> _logger;

    public InMemoryFeatureFlagService(IOptions<FeatureFlagsOptions> options, ILogger<InMemoryFeatureFlagService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled(string featureName)
    {
        if (!_options.Flags.TryGetValue(featureName, out var config))
        {
            _logger.LogWarning("Feature flag '{FeatureName}' not found, returning false", featureName);
            return false;
        }

        return config.Enabled;
    }

    public string GetVariant(string featureName, string defaultVariant = "control")
    {
        if (!_options.Flags.TryGetValue(featureName, out var config))
        {
            return defaultVariant;
        }

        return config.Variant;
    }

    public Task<bool> EvaluateAsync(string featureName, bool defaultValue = false)
    {
        return Task.FromResult(IsEnabled(featureName));
    }
}

public static class FeatureFlagsExtensions
{
    public const string EnableRecorrenciaAutomatica = "recorrencia-automatica";
    public const string EnableImportacaoWhatsapp = "importacao-whatsapp";
    public const string EnableConciliacaoAutomatica = "conciliacao-automatica";
    public const string EnableDashboardPrevisao = "dashboard-previsao";
    public const string EnableLimiteCompartilhado = "limite-compartilhado";
    public const string EnableComprasPlanejadas = "compras-planejadas";
    public const string EnableChavesPix = "chaves-pix";
    
    public static IServiceCollection AddFeatureFlags(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FeatureFlagsOptions>(configuration.GetSection(FeatureFlagsOptions.SectionName));
        services.AddSingleton<IFeatureFlagService, InMemoryFeatureFlagService>();
        return services;
    }
}