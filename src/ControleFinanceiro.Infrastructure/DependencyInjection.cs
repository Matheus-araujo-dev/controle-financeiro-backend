using ControleFinanceiro.Application.FinanceAI;
using ControleFinanceiro.Application.FinanceAI.Tools;
using ControleFinanceiro.Application.Identidade;
using ControleFinanceiro.Application.ImportacoesWhatsapp;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Events;
using ControleFinanceiro.Infrastructure.FinanceAI;
using ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;
using ControleFinanceiro.Infrastructure.Persistence;
using ControleFinanceiro.Infrastructure.Identity;
using ControleFinanceiro.Infrastructure.Events;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SqlServer")
            ?? throw new InvalidOperationException("Connection string 'SqlServer' was not configured.");

        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<IdentidadeOptions>(configuration.GetSection(IdentidadeOptions.SectionName));
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
        services.AddScoped<IAppDbContext>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());
        services.AddScoped<IStatusDbContext>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());
        services.AddScoped<ICadastrosDbContext>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());
        services.AddScoped<IFileStorage, LocalImportFileStorage>();
        services.AddScoped<IDocumentExtractor, DefaultDocumentExtractor>();
        services.AddScoped<IImportSuggestionService, HeuristicImportSuggestionService>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

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
        services.AddScoped<IWhatsappMensagemService, WhatsappMensagemService>();
        services.AddScoped<IExtracaoImagemFinanceiroService, ClaudeVisionExtracaoService>();
        services.AddScoped<AlertasVencimentoService>();

        services.Configure<WhatsappBridgeOptions>(configuration.GetSection(WhatsappBridgeOptions.SectionName));
        services.Configure<AlertasWhatsappOptions>(configuration.GetSection(AlertasWhatsappOptions.SectionName));
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));

        var llmOptions = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
        if (llmOptions.Enabled && !string.IsNullOrWhiteSpace(llmOptions.ApiKey))
        {
            services.AddHttpClient<AnthropicLlmClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.anthropic.com/v1/");
                client.DefaultRequestHeaders.Add("x-api-key", llmOptions.ApiKey);
                client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            });
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
            });
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
        });
        services.AddScoped<IWhatsappOutboundService, WhatsappOutboundService>();
        services.AddHostedService<AlertasWhatsappHostedService>();

        return services;
    }
}
