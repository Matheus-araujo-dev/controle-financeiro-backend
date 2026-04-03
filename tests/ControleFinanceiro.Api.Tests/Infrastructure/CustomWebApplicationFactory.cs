using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ControleFinanceiro.Api.Tests.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting(
            "ConnectionStrings:SqlServer",
            "Server=(localdb)\\mssqllocaldb;Database=ControleFinanceiroTests;Trusted_Connection=True;TrustServerCertificate=True;");
    }
}
