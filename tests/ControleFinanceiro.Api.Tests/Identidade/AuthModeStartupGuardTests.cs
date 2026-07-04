using ControleFinanceiro.Api.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ControleFinanceiro.Api.Tests.Identidade;

/// <summary>
/// Garante que a seleção de esquema de autenticação é fail-closed: um valor de
/// <c>Auth:Mode</c> não reconhecido, ou o modo Development fora de um ambiente de
/// desenvolvimento, é rejeitado no registro dos serviços em vez de cair
/// silenciosamente no bypass via header X-Debug-User.
/// </summary>
public sealed class AuthModeStartupGuardTests
{
    private const string SigningKey = "chave-de-teste-para-jwt-com-tamanho-suficiente-123456";

    [Theory]
    [InlineData("disable")]
    [InlineData("Disabled")]
    [InlineData("SelfJwtt")]
    [InlineData("")]
    public void AuthModeDesconhecido_DeveLancar(string mode)
    {
        var act = () => Registrar(mode, environmentName: "Development");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Auth:Mode inválido*");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void ModoDevelopment_ForaDeDesenvolvimento_DeveLancar(string environmentName)
    {
        var act = () => Registrar("Development", environmentName);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*proibido no ambiente '{environmentName}'*");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void ModoDevelopment_EmDesenvolvimento_DevePermitir(string environmentName)
    {
        var act = () => Registrar("Development", environmentName);

        act.Should().NotThrow();
    }

    [Fact]
    public void ModoSelfJwt_EmProducao_DevePermitir()
    {
        var config = new Dictionary<string, string?>
        {
            ["Auth:Mode"] = "SelfJwt",
            ["Auth:JwtSigningKey"] = SigningKey,
        };

        var act = () => Registrar(config, environmentName: "Production");

        act.Should().NotThrow();
    }

    private static void Registrar(string mode, string environmentName)
        => Registrar(new Dictionary<string, string?> { ["Auth:Mode"] = mode }, environmentName);

    private static void Registrar(Dictionary<string, string?> settings, string environmentName)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var environment = new StubHostEnvironment { EnvironmentName = environmentName };

        new ServiceCollection().AddApiFoundation(configuration, environment);
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "ControleFinanceiro.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
