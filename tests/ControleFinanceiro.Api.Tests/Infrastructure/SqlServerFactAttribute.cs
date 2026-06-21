using Xunit;

namespace ControleFinanceiro.Api.Tests.Infrastructure;

/// <summary>
/// Marca testes de integração que dependem de recursos de SQL Server não suportados pelo SQLite
/// (ex.: ORDER BY em colunas decimal). São pulados automaticamente quando a suíte roda em SQLite
/// (LocalDB indisponível ou CF_TEST_FORCE_SQLITE=1).
/// </summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!CustomWebApplicationFactory.UsesSqlServer)
        {
            Skip = "Requer SQL Server (LocalDB); pulado no provider SQLite.";
        }
    }
}
