using ControleFinanceiro.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Infrastructure.Tests.Persistence;

public sealed class DependencyInjectionTests
{
    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=cf;Username=postgres;Password=pass")]
    [InlineData("Host=db;Port=5433;Database=mydb;Username=user;Password=123")]
    public void ResolveConnectionString_QuandoFormatoKeyValue_RetornaSemAlteracao(string cs)
    {
        var result = DependencyInjection.ResolveConnectionString(cs);

        result.Should().Be(cs);
    }

    [Fact]
    public void ResolveConnectionString_QuandoUriPostgresql_ConvertePraFormatoNpgsql()
    {
        const string uri = "postgresql://myuser:mypassword@db.railway.internal:5432/railway";

        var result = DependencyInjection.ResolveConnectionString(uri);

        result.Should().Contain("Host=db.railway.internal");
        result.Should().Contain("Port=5432");
        result.Should().Contain("Database=railway");
        result.Should().Contain("Username=myuser");
        result.Should().Contain("Password=mypassword");
        result.Should().Contain("SSL Mode=Require");
    }

    [Fact]
    public void ResolveConnectionString_QuandoUriPostgres_ConvertePraFormatoNpgsql()
    {
        const string uri = "postgres://user:p%40ss@host:5432/db";

        var result = DependencyInjection.ResolveConnectionString(uri);

        result.Should().Contain("Host=host");
        result.Should().Contain("Password=p@ss");
    }

    [Fact]
    public void ResolveConnectionString_QuandoUriComSenhaCodificada_DecodificaCorreto()
    {
        const string uri = "postgresql://admin:Str%40ng%21Pass@railway.host:5432/prod";

        var result = DependencyInjection.ResolveConnectionString(uri);

        result.Should().Contain("Password=Str@ng!Pass");
    }
}
