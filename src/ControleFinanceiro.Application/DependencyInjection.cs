using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ControleFinanceiro.Application.Bootstrap;
using ControleFinanceiro.Application.Common.Cache;
using ControleFinanceiro.Application.Common.Validation;
using System.Reflection;

namespace ControleFinanceiro.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddValidatorsFromAssemblyContaining<DomainValidator<object>>(ServiceLifetime.Transient);
        
        services.AddSingleton<IBootstrapCatalogService, BootstrapCatalogService>();
        services.AddSingleton<IValidationResultFactory, ValidationResultFactory>();
        services.AddScoped<ILookupCacheService, LookupCacheService>();
        services.AddScopedApplicationServices(typeof(DependencyInjection).Assembly);
        services.AddScoped<Dashboard.DashboardDbHelpers>();
        services.AddScoped<Financeiro.ContasPagar.ContaPagarSharedHelper>();
        return services;
    }

    private static IServiceCollection AddScopedApplicationServices(
        this IServiceCollection services,
        Assembly assembly)
    {
        var applicationServiceTypes = assembly
            .GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false, IsPublic: true } &&
                (type.Name.EndsWith("AppService", StringComparison.Ordinal) ||
                 type.Name.EndsWith("Service", StringComparison.Ordinal) ||
                 type.Name.EndsWith("CommandService", StringComparison.Ordinal) ||
                 type.Name.EndsWith("EventHandler", StringComparison.Ordinal) ||
                 type.Name.EndsWith("QueryService", StringComparison.Ordinal)))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var serviceType in applicationServiceTypes)
        {
            if (!serviceType.Name.EndsWith("AppService", StringComparison.Ordinal))
            {
                var interfaces = serviceType
                    .GetInterfaces()
                    .Where(@interface => @interface.IsPublic && @interface.Name.StartsWith('I'))
                    .ToArray();

                foreach (var @interface in interfaces)
                {
                    var interfaceRegistered = services.Any(descriptor =>
                        descriptor.ServiceType == @interface &&
                        descriptor.ImplementationType == serviceType);

                    if (!interfaceRegistered)
                    {
                        services.AddScoped(@interface, serviceType);
                    }
                }
            }

            var concreteRegistered = services.Any(descriptor =>
                descriptor.ServiceType == serviceType &&
                descriptor.ImplementationType == serviceType);

            if (!concreteRegistered)
            {
                services.AddScoped(serviceType);
            }
        }

        return services;
    }
}
