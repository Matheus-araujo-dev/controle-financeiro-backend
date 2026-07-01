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
    IUnitOfWork
{
    Guid? WorkspaceCorrente { get; }

    void DefinirWorkspaceCorrente(Guid workspaceId);

    void DefinirFamiliaCorrente(Guid familiaId);
}
