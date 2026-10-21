using ControleFinanceiro.Application.Common.Extensions;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.ImportacoesWhatsapp;

public sealed class ImportacaoWhatsappSharedHelper(IAppDbContext dbContext)
{
    internal async Task<ImportacaoWhatsapp?> CarregarImportacaoAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.ImportacoesWhatsapp
            .Include(x => x.Itens)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    internal async Task<ImportacaoWhatsapp?> CarregarImportacaoPorItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var importacaoId = await dbContext.ItensImportadosWhatsapp
            .Where(x => x.Id == itemId)
            .Select(x => x.ImportacaoWhatsappId)
            .SingleOrDefaultAsync(cancellationToken);

        return importacaoId == Guid.Empty
            ? null
            : await CarregarImportacaoAsync(importacaoId, cancellationToken);
    }

    internal Task<bool> ImportacaoJaPossuiGeracaoFinanceiraAsync(Guid importacaoId, CancellationToken cancellationToken)
    {
        return dbContext.ContasPagar.AnyAsync(x => x.OrigemImportacaoWhatsappId == importacaoId, cancellationToken);
    }

    internal async Task RemoverGeracaoFinanceiraDaImportacaoAsync(Guid importacaoId, CancellationToken cancellationToken)
    {
        var contasGeradas = await dbContext.ContasPagar
            .Where(x => x.OrigemImportacaoWhatsappId == importacaoId)
            .ToListAsync(cancellationToken);

        if (contasGeradas.Count == 0)
        {
            return;
        }

        var contaIds = contasGeradas.Select(x => x.Id).ToArray();
        var movimentos = await dbContext.MovimentacoesFinanceiras
            .Where(x => x.ContaPagarId.HasValue)
            .WhereIn(x => x.ContaPagarId!.Value, contaIds)
            .ToListAsync(cancellationToken);
        var rateios = await dbContext.RateiosContaGerencial
            .Where(x => x.ContaPagarId.HasValue)
            .WhereIn(x => x.ContaPagarId!.Value, contaIds)
            .ToListAsync(cancellationToken);

        dbContext.MovimentacoesFinanceiras.RemoveRange(movimentos);
        dbContext.RateiosContaGerencial.RemoveRange(rateios);
        dbContext.ContasPagar.RemoveRange(contasGeradas);
    }
}
