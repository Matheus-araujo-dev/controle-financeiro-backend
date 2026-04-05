using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Cadastros.FormasPagamento;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Common.Persistence;

public interface IAppDbContext
{
    DbSet<Pessoa> Pessoas { get; }

    DbSet<FormaPagamento> FormasPagamento { get; }

    DbSet<ContaBancaria> ContasBancarias { get; }

    DbSet<Cartao> Cartoes { get; }

    DbSet<ContaGerencial> ContasGerenciais { get; }

    DbSet<StatusConta> StatusContas { get; }

    DbSet<StatusMovimentacao> StatusMovimentacoes { get; }

    DbSet<ContaPagar> ContasPagar { get; }

    DbSet<ContaReceber> ContasReceber { get; }

    DbSet<RateioContaGerencial> RateiosContaGerencial { get; }

    DbSet<MovimentacaoFinanceira> MovimentacoesFinanceiras { get; }

    DbSet<FaturaCartao> FaturasCartao { get; }

    DbSet<RegraRecorrencia> RegrasRecorrencia { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
