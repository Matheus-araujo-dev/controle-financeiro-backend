using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ControleFinanceiro.Api.Tests.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DevelopmentUserHeader = "X-Debug-User";

    // Provider preferido: PostgreSQL real (via CF_TEST_POSTGRES ou localhost padrão).
    // Sem ele (ou com CF_TEST_FORCE_SQLITE=1), cai para SQLite in-memory — e os testes marcados com
    // [PostgresFactAttribute] são pulados.
    private static readonly Lazy<string?> PostgresTemplate = new(DetectarPostgres);

    /// <summary>True quando os testes rodam contra PostgreSQL real; false em SQLite.</summary>
    public static bool UsesPostgres => PostgresTemplate.Value is not null;

    private readonly Action<IServiceCollection>? _configureAdditionalServices;
    private readonly string? _pgConnectionString;
    private SqliteConnection? _sqliteConnection;

    public CustomWebApplicationFactory()
        : this(null)
    {
    }

    internal CustomWebApplicationFactory(Action<IServiceCollection>? configureAdditionalServices = null)
    {
        _configureAdditionalServices = configureAdditionalServices;
        _pgConnectionString = PostgresTemplate.Value?.Replace("{DB}", $"cf_api_tests_{Guid.NewGuid():N}");
    }

    private static string? DetectarPostgres()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("CF_TEST_FORCE_SQLITE"), "1", StringComparison.Ordinal))
        {
            return null;
        }

        var template = Environment.GetEnvironmentVariable("CF_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(template))
        {
            template = "Host=localhost;Port=5432;Database={DB};Username=postgres;Password=ChangeMe123!";
        }

        try
        {
            var cs = template.Replace("{DB}", "postgres");
            using var connection = new NpgsqlConnection(cs);
            connection.Open();
            return template;
        }
        catch
        {
            return null; // PostgreSQL indisponível → SQLite
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _pgConnectionString ?? "Host=unused;Database=unused;");
        builder.UseSetting("Auth:Mode", "Development");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();

            var descriptorsToRemove = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    descriptor.ServiceType == typeof(AppDbContext) ||
                    descriptor.ServiceType == typeof(IAppDbContext) ||
                    (descriptor.ServiceType.IsGenericType &&
                     descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>) &&
                     descriptor.ServiceType.GenericTypeArguments[0] == typeof(AppDbContext)))
                .ToArray();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            if (_pgConnectionString is not null)
            {
                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(_pgConnectionString, pg => pg.CommandTimeout(180)));
            }
            else
            {
                _sqliteConnection = new SqliteConnection("DataSource=:memory:");
                _sqliteConnection.Open();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_sqliteConnection));
            }

            services.AddScoped<IAppDbContext>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());
            _configureAdditionalServices?.Invoke(services);

            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.EnsureCreated();
        });
    }

    public async Task InitializeAsync()
    {
        await ResetDatabaseAsync();
    }

    public new async Task DisposeAsync()
    {
        if (_pgConnectionString is not null)
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureDeletedAsync();
        }

        if (_sqliteConnection is not null)
        {
            await _sqliteConnection.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public new HttpClient CreateClient()
    {
        return CreateAuthenticatedClient("test-user");
    }

    public new HttpClient CreateClient(WebApplicationFactoryClientOptions options)
    {
        var client = base.CreateClient(options);
        Authenticate(client, "test-user");
        return client;
    }

    public HttpClient CreateAuthenticatedClient(string user = "test-user")
    {
        var client = base.CreateClient();
        Authenticate(client, user);
        return client;
    }

    public HttpClient CreateAnonymousClient()
    {
        var client = base.CreateClient();
        client.DefaultRequestHeaders.Remove(DevelopmentUserHeader);
        return client;
    }

    private static void Authenticate(HttpClient client, string user)
    {
        client.DefaultRequestHeaders.Remove(DevelopmentUserHeader);
        client.DefaultRequestHeaders.Add(DevelopmentUserHeader, user);
    }
}
