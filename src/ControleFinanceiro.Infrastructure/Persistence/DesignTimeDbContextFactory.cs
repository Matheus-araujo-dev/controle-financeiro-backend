using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ControleFinanceiro.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DefaultConnectionString =
        "Server=localhost,1433;Database=ControleFinanceiro;User Id=sa;Password=ChangeMe123!;TrustServerCertificate=True;";

    public AppDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(ResolveApiProjectPath())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("SqlServer")
            ?? DefaultConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(
            connectionString,
            sqlOptions => sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));

        return new AppDbContext(optionsBuilder.Options);
    }

    private static string ResolveApiProjectPath()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "src", "ControleFinanceiro.Api"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "ControleFinanceiro.Api"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "src", "ControleFinanceiro.Api"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ControleFinanceiro.Api")
        }
        .Select(Path.GetFullPath)
        .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "appsettings.json")))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not resolve the ControleFinanceiro.Api configuration directory.");
    }
}
