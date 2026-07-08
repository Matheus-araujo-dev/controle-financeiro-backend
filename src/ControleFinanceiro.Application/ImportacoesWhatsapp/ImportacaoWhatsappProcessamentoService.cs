using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Application.Identidade;
using ControleFinanceiro.Contracts.ImportacoesWhatsapp;
using ControleFinanceiro.Domain.FinanceAI;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Application.ImportacoesWhatsapp;

public interface IImportacaoWhatsappProcessamentoService
{
    Task<ImportacaoWhatsappDetalheResponse> ReceberWebhookAsync(
        ReceberImportacaoWhatsappWebhookRequest request,
        CancellationToken cancellationToken);

    Task<ImportacaoWhatsappDetalheResponse?> ReprocessarAsync(
        Guid id,
        CancellationToken cancellationToken);
}

public sealed class ImportacaoWhatsappProcessamentoService(
    IAppDbContext dbContext,
    IFileStorage fileStorage,
    IDocumentExtractor documentExtractor,
    IImportSuggestionService importSuggestionService,
    IImportacaoWhatsappQueryService queryService,
    ICurrentUser currentUser,
    IOptions<IdentidadeOptions> identidadeOptions,
    ImportacaoWhatsappSharedHelper helper) : IImportacaoWhatsappProcessamentoService
{
    private static readonly string[] MimeTypesSuportados =
    [
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/webp",
        "text/plain"
    ];

    public async Task<ImportacaoWhatsappDetalheResponse> ReceberWebhookAsync(
        ReceberImportacaoWhatsappWebhookRequest request,
        CancellationToken cancellationToken)
    {
        ValidarWebhook(request);

        var familiaId = await ResolverFamiliaWebhookAsync(request.Remetente, cancellationToken);
        dbContext.DefinirFamiliaCorrente(familiaId);

        var tipoOrigem = MapearTipoOrigem(request.TipoOrigem);
        var importacao = ImportacaoWhatsapp.CriarRecebida(
            tipoOrigem,
            request.Remetente,
            request.TextoBruto,
            request.NomeArquivo,
            null,
            request.MimeType);

        if (!string.IsNullOrWhiteSpace(request.ArquivoBase64))
        {
            try
            {
                var fileStorageResult = await fileStorage.SaveAsync(
                    new FileStorageRequest(
                        importacao.Id,
                        request.NomeArquivo!,
                        request.MimeType!,
                        request.ArquivoBase64),
                    cancellationToken);
                importacao.RegistrarArtefatoArmazenado(fileStorageResult.CaminhoArquivo);
            }
            catch (ArgumentException exception)
            {
                throw ValidationExceptionFactory.Create("ArquivoBase64", exception.Message);
            }
        }

        dbContext.ImportacoesWhatsapp.Add(importacao);
        await ProcessarImportacaoAsync(importacao, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await queryService.ObterPorIdAsync(importacao.Id, cancellationToken)
            ?? throw new InvalidOperationException("Falha ao recuperar a importação processada.");
    }

    public async Task<ImportacaoWhatsappDetalheResponse?> ReprocessarAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var importacao = await helper.CarregarImportacaoAsync(id, cancellationToken);
        if (importacao is null)
        {
            return null;
        }

        if (importacao.Status == StatusImportacaoWhatsapp.Confirmado)
        {
            throw ValidationExceptionFactory.Create("Status", "Reabra a importação antes de reprocessar.");
        }

        if (importacao.Itens.Count > 0)
        {
            dbContext.ItensImportadosWhatsapp.RemoveRange(importacao.Itens);
            importacao.SubstituirItens([]);
        }

        await ProcessarImportacaoAsync(importacao, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await queryService.ObterPorIdAsync(id, cancellationToken);
    }

    private async Task<Guid> ResolverFamiliaWebhookAsync(string remetente, CancellationToken cancellationToken)
    {
        if (currentUser.FamiliaId is { } familiaAutenticada)
        {
            return familiaAutenticada;
        }

        var telefone = WhatsappUsuario.NormalizarTelefone(remetente);
        var familiaDoRemetente = await dbContext.WhatsappUsuarios
            .AsNoTracking()
            .Where(usuario => usuario.Telefone == telefone && usuario.Ativo)
            .Select(usuario => (Guid?)usuario.FamiliaId)
            .FirstOrDefaultAsync(cancellationToken);

        if (familiaDoRemetente.HasValue)
        {
            return familiaDoRemetente.Value;
        }

        if (identidadeOptions.Value.FamiliaPadraoId is { } familiaPadraoId)
        {
            return familiaPadraoId;
        }

        throw ValidationExceptionFactory.Create("Remetente", "Remetente não está vinculado a uma família ativa.");
    }

    private async Task ProcessarImportacaoAsync(ImportacaoWhatsapp importacao, CancellationToken cancellationToken)
    {
        importacao.MarcarEmProcessamento();
        try
        {
            var extractionResult = await documentExtractor.ExtractAsync(
                new DocumentExtractionRequest(
                    importacao.TextoBruto,
                    importacao.NomeArquivo,
                    importacao.MimeType,
                    importacao.CaminhoArquivo),
                cancellationToken);

            if (!extractionResult.Success || string.IsNullOrWhiteSpace(extractionResult.TextoExtraido))
            {
                importacao.RegistrarErroExtracao(extractionResult.MensagemErro ?? "Não foi possível extrair conteúdo da importação.");
                return;
            }

            importacao.RegistrarExtracaoComSucesso(extractionResult.Confianca);

            var itens = await importSuggestionService.GenerateAsync(
                new ImportSuggestionRequest(
                    importacao.TipoOrigem,
                    importacao.Remetente,
                    extractionResult.TextoExtraido,
                    importacao.NomeArquivo,
                    importacao.MimeType),
                cancellationToken);

            var itensGerados = itens
                .Select(item =>
                {
                    var payload = ImportacaoWhatsappSuggestionPayload.Parse(item.PayloadSugeridoJson);
                    return ItemImportadoWhatsapp.Criar(
                        importacao.Id,
                        item.TipoSugestao,
                        item.PayloadSugeridoJson,
                        payload.BuildLearningKey());
                })
                .ToArray();

            importacao.SubstituirItens(itensGerados);
            dbContext.ItensImportadosWhatsapp.AddRange(itensGerados);
        }
        catch (Exception)
        {
            importacao.RegistrarErroExtracao("Falha ao integrar com o extrator ou a heuristica da importacao.");
        }
    }

    private static void ValidarWebhook(ReceberImportacaoWhatsappWebhookRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Remetente))
        {
            throw ValidationExceptionFactory.Create("Remetente", "Remetente é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request.TextoBruto) && string.IsNullOrWhiteSpace(request.ArquivoBase64))
        {
            throw ValidationExceptionFactory.Create("TextoBruto", "Informe texto bruto ou arquivo para processar a importacao.");
        }

        if (!string.IsNullOrWhiteSpace(request.ArquivoBase64))
        {
            if (string.IsNullOrWhiteSpace(request.NomeArquivo))
            {
                throw ValidationExceptionFactory.Create("NomeArquivo", "Nome do arquivo é obrigatório quando houver artefato.");
            }

            if (string.IsNullOrWhiteSpace(request.MimeType))
            {
                throw ValidationExceptionFactory.Create("MimeType", "Mime type é obrigatório quando houver artefato.");
            }

            if (!MimeTypesSuportados.Contains(request.MimeType.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                throw ValidationExceptionFactory.Create("MimeType", "Mime type não suportado para importação.");
            }
        }
    }

    private static TipoOrigemImportacaoWhatsapp MapearTipoOrigem(TipoOrigemImportacaoWhatsappRequest tipoOrigem)
    {
        return tipoOrigem switch
        {
            TipoOrigemImportacaoWhatsappRequest.Texto => TipoOrigemImportacaoWhatsapp.Texto,
            TipoOrigemImportacaoWhatsappRequest.Imagem => TipoOrigemImportacaoWhatsapp.Imagem,
            TipoOrigemImportacaoWhatsappRequest.Pdf => TipoOrigemImportacaoWhatsapp.Pdf,
            TipoOrigemImportacaoWhatsappRequest.Arquivo => TipoOrigemImportacaoWhatsapp.Arquivo,
            _ => throw new ApplicationValidationException("Um ou mais campos são inválidos.", new Dictionary<string, string[]>
            {
                ["TipoOrigem"] = ["Tipo de origem inválido."]
            })
        };
    }
}
