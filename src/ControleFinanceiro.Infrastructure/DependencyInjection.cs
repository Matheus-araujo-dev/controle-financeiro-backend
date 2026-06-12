using ControleFinanceiro.Application.Identidade;
using ControleFinanceiro.Application.ImportacoesWhatsapp;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Events;
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

        return services;
    }
}
