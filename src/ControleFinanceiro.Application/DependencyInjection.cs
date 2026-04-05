using Microsoft.Extensions.DependencyInjection;
using ControleFinanceiro.Application.Bootstrap;
using ControleFinanceiro.Application.Cadastros.Cartoes;
using ControleFinanceiro.Application.Cadastros.ContasBancarias;
using ControleFinanceiro.Application.Cadastros.ContasGerenciais;
using ControleFinanceiro.Application.Cadastros.FormasPagamento;
using ControleFinanceiro.Application.Cadastros.Pessoas;
using ControleFinanceiro.Application.Dashboard;
using ControleFinanceiro.Application.Financeiro.ContasPagar;
using ControleFinanceiro.Application.Financeiro.ContasReceber;
using ControleFinanceiro.Application.Financeiro.Faturas;
using ControleFinanceiro.Application.Financeiro.Movimentacoes;

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
        services.AddScoped<ContaPagarAppService>();
        services.AddScoped<ContaReceberAppService>();
        services.AddScoped<FaturaCartaoAppService>();
        services.AddScoped<MovimentacaoFinanceiraAppService>();
        services.AddScoped<DashboardAppService>();
        return services;
    }
}
