using ControleFinanceiro.Application.Bootstrap;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Application.Tests.Common;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_ShouldRegisterAllAppServicesAsScoped()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        var appServiceTypes = typeof(DependencyInjection).Assembly
            .GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false, IsPublic: true } &&
                type.Name.EndsWith("AppService", StringComparison.Ordinal))
            .ToArray();

        appServiceTypes.Should().NotBeEmpty();

        foreach (var appServiceType in appServiceTypes)
        {
            var descriptor = services.SingleOrDefault(candidate =>
                candidate.ServiceType == appServiceType &&
                candidate.ImplementationType == appServiceType);

            descriptor.Should().NotBeNull($"{appServiceType.Name} should be registered as scoped");
            descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
        }
    }

    [Fact]
    public void AddApplication_ShouldKeepBootstrapCatalogServiceContractRegistered()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        var descriptor = services.SingleOrDefault(candidate =>
            candidate.ServiceType == typeof(IBootstrapCatalogService) &&
            candidate.ImplementationType == typeof(BootstrapCatalogService));

        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }
}
