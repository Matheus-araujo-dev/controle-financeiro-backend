using ControleFinanceiro.Application.Cadastros.ContasGerenciais;
using ControleFinanceiro.Application.Cadastros.Pessoas;
using ControleFinanceiro.Application.Dashboard;
using ControleFinanceiro.Application.FinanceAI;
using ControleFinanceiro.Application.FinanceAI.Tools;
using ControleFinanceiro.Application.Financeiro.ContasPagar;
using ControleFinanceiro.Application.Financeiro.ContasReceber;
using ControleFinanceiro.Application.Identidade;
using ControleFinanceiro.Application.ImportacoesWhatsapp;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Events;
using ControleFinanceiro.Domain.Financeiro.Events;
using ControleFinanceiro.Infrastructure.Events;
using ControleFinanceiro.Infrastructure.Events.Handlers;
using ControleFinanceiro.Infrastructure.FinanceAI;
using ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;
using ControleFinanceiro.Infrastructure.Persistence;
using ControleFinanceiro.Infrastructure.Persistence.Repositories;
using ControleFinanceiro.Infrastructure.Identity;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = ResolveConnectionString(
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not configured."));

        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "CF:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<IdentidadeOptions>(configuration.GetSection(IdentidadeOptions.SectionName));
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                    npgsqlOptions.CommandTimeout(30);
                }));
        services.AddScoped<IAppDbContext>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());
        services.AddScoped<IContaPagarRepository, ContaPagarRepository>();
        services.AddScoped<IContaReceberRepository, ContaReceberRepository>();
        services.AddScoped<IPessoaRepository, PessoaRepository>();
        services.AddScoped<IContaGerencialRepository, ContaGerencialRepository>();
        services.AddScoped<IStatusDbContext>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());
        services.AddScoped<ICadastrosDbContext>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());
        services.AddScoped<IFileStorage, LocalImportFileStorage>();
        services.AddScoped<IDocumentExtractor, DefaultDocumentExtractor>();
        services.AddScoped<IImportSuggestionService, HeuristicImportSuggestionService>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IDomainEventHandler<ContaPagarCriadaEvent>, ContaPagarCriadaEventHandler>();
        services.AddScoped<IDomainEventHandler<ContaPagarLiquidadaEvent>, ContaPagarLiquidadaEventHandler>();
        services.AddScoped<IDomainEventHandler<ContaReceberRecebidaEvent>, ContaReceberRecebidaEventHandler>();

        // Agente IA — ferramentas (cada IFinanceTool é resolvida por IEnumerable<IFinanceTool>)
        services.AddScoped<IFinanceTool, ListarCategoriasTool>();
        services.AddScoped<IFinanceTool, BuscarSaldoAtualTool>();
        services.AddScoped<IFinanceTool, BuscarResumoMensalTool>();
        services.AddScoped<IFinanceTool, BuscarGastosPorCategoriaTool>();
        services.AddScoped<IFinanceTool, BuscarGastosPorResponsavelTool>();
        services.AddScoped<IFinanceTool, ListarPessoasTool>();
        services.AddScoped<IFinanceTool, ListarMeiosPagamentoTool>();
        services.AddScoped<IFinanceTool, CriarLancamentoTool>();
        services.AddScoped<IFinanceAgentService, FinanceAgentService>();
        services.AddScoped<FinanceInsightsService>();
        services.AddScoped<FinanceCategorizacaoService>();
        services.AddScoped<IWhatsappMensagemService, WhatsappMensagemService>();
        services.AddScoped<IExtracaoImagemFinanceiroService, ClaudeVisionExtracaoService>();
        services.AddScoped<AlertasVencimentoService>();

        services.Configure<WhatsappBridgeOptions>(configuration.GetSection(WhatsappBridgeOptions.SectionName));
        services.Configure<AlertasWhatsappOptions>(configuration.GetSection(AlertasWhatsappOptions.SectionName));
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));

        // Registro único de políticas de resiliência (retry + circuit breaker compartilhado por cliente).
        services.AddPolicyRegistry((sp, registry) =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            foreach (var client in new[] { "Anthropic", "Whisper", "WhatsappBridge" })
            {
                var logger = loggerFactory.CreateLogger($"HttpResilience.{client}");
                registry.Add($"{client}.retry", HttpResiliencePolicies.RetryPolicy(logger, client));
                registry.Add($"{client}.cb", HttpResiliencePolicies.CircuitBreakerPolicy(logger, client));
            }
        });

        var llmOptions = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
        if (llmOptions.Enabled && !string.IsNullOrWhiteSpace(llmOptions.ApiKey))
        {
            services.AddHttpClient<AnthropicLlmClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.anthropic.com/v1/");
                client.DefaultRequestHeaders.Add("x-api-key", llmOptions.ApiKey);
                client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                client.Timeout = TimeSpan.FromSeconds(60);
            })
                .AddPolicyHandlerFromRegistry("Anthropic.retry")
                .AddPolicyHandlerFromRegistry("Anthropic.cb");
            services.AddScoped<ILlmClient>(sp => sp.GetRequiredService<AnthropicLlmClient>());
            services.AddScoped<ILlmVisionClient>(sp => sp.GetRequiredService<AnthropicLlmClient>());
        }
        else
        {
            services.AddScoped<FakeLlmClient>();
            services.AddScoped<ILlmClient>(sp => sp.GetRequiredService<FakeLlmClient>());
            services.AddScoped<ILlmVisionClient>(sp => sp.GetRequiredService<FakeLlmClient>());
        }

        var openAiOptions = configuration.GetSection(OpenAiOptions.SectionName).Get<OpenAiOptions>() ?? new OpenAiOptions();
        if (!string.IsNullOrWhiteSpace(openAiOptions.ApiKey))
        {
            services.AddHttpClient<WhisperTranscricaoService>(client =>
            {
                client.BaseAddress = new Uri("https://api.openai.com/v1/");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiOptions.ApiKey}");
                client.Timeout = TimeSpan.FromSeconds(120);
            })
                .AddPolicyHandlerFromRegistry("Whisper.retry")
                .AddPolicyHandlerFromRegistry("Whisper.cb");
            services.AddScoped<ITranscricaoAudioService, WhisperTranscricaoService>();
        }
        else
        {
            services.AddScoped<ITranscricaoAudioService, FakeTranscricaoService>();
        }

        // Outbound WhatsApp — chama o bridge Node.js para mensagens proativas
        var bridgeOptions = configuration.GetSection(WhatsappBridgeOptions.SectionName).Get<WhatsappBridgeOptions>() ?? new WhatsappBridgeOptions();
        services.AddHttpClient<WhatsappOutboundService>(client =>
        {
            client.BaseAddress = new Uri(bridgeOptions.OutboundUrl.TrimEnd('/') + "/");
            if (!string.IsNullOrWhiteSpace(bridgeOptions.ApiKey))
                client.DefaultRequestHeaders.Add("X-Internal-ApiKey", bridgeOptions.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(15);
        })
            .AddPolicyHandlerFromRegistry("WhatsappBridge.retry")
            .AddPolicyHandlerFromRegistry("WhatsappBridge.cb");
        services.AddScoped<IWhatsappOutboundService, WhatsappOutboundService>();
        services.AddHostedService<AlertasWhatsappHostedService>();

        return services;
    }

    // Railway injects DATABASE_URL as a postgres:// URI; Npgsql expects key=value format.
    private static string ResolveConnectionString(string cs)
    {
        if (!cs.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !cs.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return cs;

        var uri = new Uri(cs);
        var userInfo = uri.UserInfo.Split(':', 2);
        var db = uri.AbsolutePath.TrimStart('/');
        return $"Host={uri.Host};Port={uri.Port};Database={db};Username={userInfo[0]};Password={Uri.UnescapeDataString(userInfo[1])};SSL Mode=Require;Trust Server Certificate=true";
    }
}
