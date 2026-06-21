using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Api.Tests.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DevelopmentUserHeader = "X-Debug-User";

    // Provider preferido: SQL Server real (LocalDB), que suporta ORDER BY decimal. Detectado uma vez.
    // Sem ele (ou com CF_TEST_FORCE_SQLITE=1), cai para SQLite in-memory — e os testes marcados com
    // [SqlServerFact] são pulados. CI pode apontar outro SQL Server via CF_TEST_SQLSERVER (com {DB}).
    private static readonly Lazy<string?> SqlServerTemplate = new(DetectarSqlServer);

    /// <summary>True quando os testes rodam contra SQL Server real (LocalDB/CI); false em SQLite.</summary>
    public static bool UsesSqlServer => SqlServerTemplate.Value is not null;

    private readonly Action<IServiceCollection>? _configureAdditionalServices;
    private readonly string? _sqlConnectionString;
    private SqliteConnection? _sqliteConnection;

    public CustomWebApplicationFactory()
        : this(null)
    {
    }

    internal CustomWebApplicationFactory(Action<IServiceCollection>? configureAdditionalServices = null)
    {
        _configureAdditionalServices = configureAdditionalServices;
        _sqlConnectionString = SqlServerTemplate.Value?.Replace("{DB}", $"CF_ApiTests_{Guid.NewGuid():N}");
    }

    private static string? DetectarSqlServer()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("CF_TEST_FORCE_SQLITE"), "1", StringComparison.Ordinal))
        {
            return null;
        }

        var template = Environment.GetEnvironmentVariable("CF_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(template))
        {
            template = "Server=(localdb)\\MSSQLLocalDB;Database={DB};Trusted_Connection=True;" +
                       "MultipleActiveResultSets=true;TrustServerCertificate=True;Connect Timeout=30";
        }

        try
        {
            using var connection = new SqlConnection(template.Replace("{DB}", "master"));
            connection.Open();
            return template;
        }
        catch
        {
            return null; // SQL Server indisponível → SQLite
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // AddInfrastructure exige uma connection string; o DbContext real é substituído abaixo.
        builder.UseSetting("ConnectionStrings:SqlServer", _sqlConnectionString ?? "Server=unused;Database=unused;");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });
        builder.ConfigureServices(services =>
        {
            // Os workers de background (recorrência, atualização de status) não devem rodar nos testes:
            // disparam no startup do host e contendem com o LocalDB do setup, gerando flakiness. São
            // cobertos por testes diretos (ver BackgroundWorkersTests).
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

            if (_sqlConnectionString is not null)
            {
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(_sqlConnectionString, sql => sql.CommandTimeout(180)));
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
        if (_sqlConnectionString is not null)
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
