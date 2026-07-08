using ControleFinanceiro.Contracts.ImportacoesWhatsapp;

namespace ControleFinanceiro.Application.ImportacoesWhatsapp;

public interface IImportacaoWhatsappCommandService
{
    Task<ImportacaoWhatsappDetalheResponse> ReceberWebhookAsync(
        ReceberImportacaoWhatsappWebhookRequest request,
        CancellationToken cancellationToken);

    Task<ImportacaoWhatsappDetalheResponse?> ReprocessarAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ImportacaoWhatsappDetalheResponse?> AprovarImportacaoAsync(
        Guid id,
        AprovarImportacaoWhatsappRequest? request,
        CancellationToken cancellationToken);

    Task<ImportacaoWhatsappDetalheResponse?> CompletarFechamentoFaturaAsync(
        Guid id,
        AprovarImportacaoWhatsappRequest request,
        CancellationToken cancellationToken);

    Task<ImportacaoWhatsappDetalheResponse?> ReabrirImportacaoAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ImportacaoWhatsappDetalheResponse?> ConfirmarItemAsync(
        Guid itemId,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken);

    Task<ImportacaoWhatsappDetalheResponse?> RejeitarItemAsync(
        Guid itemId,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken);
}

/// <summary>Thin facade kept for stability; delegates to focused processing, review and approval services.</summary>
public sealed class ImportacaoWhatsappCommandService(
    IImportacaoWhatsappProcessamentoService processamentoService,
    IImportacaoWhatsappRevisaoService revisaoService,
    IImportacaoWhatsappAprovacaoService aprovacaoService) : IImportacaoWhatsappCommandService
{
    public Task<ImportacaoWhatsappDetalheResponse> ReceberWebhookAsync(
        ReceberImportacaoWhatsappWebhookRequest request,
        CancellationToken cancellationToken) =>
        processamentoService.ReceberWebhookAsync(request, cancellationToken);

    public Task<ImportacaoWhatsappDetalheResponse?> ReprocessarAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        processamentoService.ReprocessarAsync(id, cancellationToken);

    public Task<ImportacaoWhatsappDetalheResponse?> AprovarImportacaoAsync(
        Guid id,
        AprovarImportacaoWhatsappRequest? request,
        CancellationToken cancellationToken) =>
        aprovacaoService.AprovarImportacaoAsync(id, request, cancellationToken);

    public Task<ImportacaoWhatsappDetalheResponse?> CompletarFechamentoFaturaAsync(
        Guid id,
        AprovarImportacaoWhatsappRequest request,
        CancellationToken cancellationToken) =>
        aprovacaoService.CompletarFechamentoFaturaAsync(id, request, cancellationToken);

    public Task<ImportacaoWhatsappDetalheResponse?> ReabrirImportacaoAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        aprovacaoService.ReabrirImportacaoAsync(id, cancellationToken);

    public Task<ImportacaoWhatsappDetalheResponse?> ConfirmarItemAsync(
        Guid itemId,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken) =>
        revisaoService.ConfirmarItemAsync(itemId, request, cancellationToken);

    public Task<ImportacaoWhatsappDetalheResponse?> RejeitarItemAsync(
        Guid itemId,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken) =>
        revisaoService.RejeitarItemAsync(itemId, request, cancellationToken);
}
