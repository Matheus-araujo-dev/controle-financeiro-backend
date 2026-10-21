using Xunit;

namespace ControleFinanceiro.Api.Tests.Infrastructure;

/// <summary>
/// Marca testes de integração que dependem de recursos PostgreSQL não suportados pelo SQLite
/// (ex.: ORDER BY em colunas decimal, tipos específicos). São pulados automaticamente quando a
/// suíte roda em SQLite (PostgreSQL indisponível ou CF_TEST_FORCE_SQLITE=1).
/// </summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!CustomWebApplicationFactory.UsesPostgres)
        {
            Skip = "Requer PostgreSQL; pulado no provider SQLite.";
        }
    }
}
