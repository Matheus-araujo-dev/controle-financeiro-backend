using ControleFinanceiro.Domain.FinanceAI;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Common.Persistence;

public interface IFinanceAiDbContext : IUnitOfWork
{
    DbSet<AiConversa> AiConversas { get; }
    DbSet<AiMensagem> AiMensagens { get; }
    DbSet<AiToolCall> AiToolCalls { get; }
}
