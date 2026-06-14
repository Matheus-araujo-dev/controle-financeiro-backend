using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Api.Tests.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DevelopmentUserHeader = "X-Debug-User";
    private readonly Action<IServiceCollection>? _configureAdditionalServices;
    private SqliteConnection? _connection;

    public CustomWebApplicationFactory()
        : this(null)
    {
    }

    internal CustomWebApplicationFactory(Action<IServiceCollection>? configureAdditionalServices = null)
    {
        _configureAdditionalServices = configureAdditionalServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });
        builder.ConfigureServices(services =>
        {
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

            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
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
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
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
