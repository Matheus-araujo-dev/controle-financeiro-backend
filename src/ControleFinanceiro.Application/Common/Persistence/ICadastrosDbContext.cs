using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Cadastros.FormasPagamento;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Common.Persistence;

public interface ICadastrosDbContext
{
    DbSet<Pessoa> Pessoas { get; }
    DbSet<PessoaChavePix> PessoasChavesPix { get; }
    DbSet<FormaPagamento> FormasPagamento { get; }
    DbSet<ContaBancaria> ContasBancarias { get; }
    DbSet<Cartao> Cartoes { get; }
    DbSet<ContaGerencial> ContasGerenciais { get; }
}

public interface IReadOnlyCadastrosDbContext
{
    IQueryable<Pessoa> Pessoas { get; }
    IQueryable<PessoaChavePix> PessoasChavesPix { get; }
    IQueryable<FormaPagamento> FormasPagamento { get; }
    IQueryable<ContaBancaria> ContasBancarias { get; }
    IQueryable<Cartao> Cartoes { get; }
    IQueryable<ContaGerencial> ContasGerenciais { get; }
}
