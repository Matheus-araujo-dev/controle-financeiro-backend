using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.ImportacoesWhatsapp;

namespace ControleFinanceiro.Application.ImportacoesWhatsapp;

public sealed class ImportacoesWhatsappAppService(
    IImportacaoWhatsappQueryService queryService,
    IImportacaoWhatsappCommandService commandService) : IImportacaoWhatsappQueryService, IImportacaoWhatsappCommandService
{
    public Task<PagedResult<ImportacaoWhatsappResumoResponse>> ListarAsync(
        ImportacaoWhatsappListQueryRequest query,
        CancellationToken cancellationToken)
    {
        return queryService.ListarAsync(query, cancellationToken);
    }

    public Task<ImportacaoWhatsappDetalheResponse?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return queryService.ObterPorIdAsync(id, cancellationToken);
    }

    public Task<ImportacaoWhatsappDetalheResponse> ReceberWebhookAsync(
        ReceberImportacaoWhatsappWebhookRequest request,
        CancellationToken cancellationToken)
    {
        return commandService.ReceberWebhookAsync(request, cancellationToken);
    }

    public Task<ImportacaoWhatsappDetalheResponse?> ReprocessarAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return commandService.ReprocessarAsync(id, cancellationToken);
    }

    public Task<ImportacaoWhatsappDetalheResponse?> AprovarImportacaoAsync(
        Guid id,
        AprovarImportacaoWhatsappRequest? request,
        CancellationToken cancellationToken)
    {
        return commandService.AprovarImportacaoAsync(id, request, cancellationToken);
    }

    public Task<ImportacaoWhatsappDetalheResponse?> CompletarFechamentoFaturaAsync(
        Guid id,
        AprovarImportacaoWhatsappRequest request,
        CancellationToken cancellationToken)
    {
        return commandService.CompletarFechamentoFaturaAsync(id, request, cancellationToken);
    }

    public Task<ImportacaoWhatsappDetalheResponse?> ReabrirImportacaoAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return commandService.ReabrirImportacaoAsync(id, cancellationToken);
    }

    public Task<ImportacaoWhatsappDetalheResponse?> ConfirmarItemAsync(
        Guid itemId,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken)
    {
        return commandService.ConfirmarItemAsync(itemId, request, cancellationToken);
    }

    public Task<ImportacaoWhatsappDetalheResponse?> RejeitarItemAsync(
        Guid itemId,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken)
    {
        return commandService.RejeitarItemAsync(itemId, request, cancellationToken);
    }
}
