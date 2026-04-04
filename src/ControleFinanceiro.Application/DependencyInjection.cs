using Microsoft.Extensions.DependencyInjection;
using ControleFinanceiro.Application.Bootstrap;
using ControleFinanceiro.Application.Cadastros.Cartoes;
using ControleFinanceiro.Application.Cadastros.ContasBancarias;
using ControleFinanceiro.Application.Cadastros.ContasGerenciais;
using ControleFinanceiro.Application.Cadastros.FormasPagamento;
using ControleFinanceiro.Application.Cadastros.Pessoas;

namespace ControleFinanceiro.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IBootstrapCatalogService, BootstrapCatalogService>();
        services.AddScoped<PessoaAppService>();
        services.AddScoped<FormaPagamentoAppService>();
        services.AddScoped<ContaBancariaAppService>();
        services.AddScoped<CartaoAppService>();
        services.AddScoped<ContaGerencialAppService>();
        return services;
    }
}
