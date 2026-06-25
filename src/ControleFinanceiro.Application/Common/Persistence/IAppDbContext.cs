using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Cadastros.FormasPagamento;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using ControleFinanceiro.Domain.PlanejamentoCompras;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Common.Persistence;

public interface IAppDbContext :
    ICadastrosDbContext,
    IFinanceiroDbContext,
    IPlanejamentoDbContext,
    IImportacoesDbContext,
    IStatusDbContext,
    IIdentidadeDbContext,
    IFinanceAiDbContext,
    IWhatsappDbContext,
    IAnexosDbContext,
    IUnitOfWork
{
    /// <summary>
    /// Define a família (tenant) corrente fora de um request autenticado
    /// (workers em background e webhooks anônimos).
    /// </summary>
    void DefinirFamiliaCorrente(Guid familiaId);
}
