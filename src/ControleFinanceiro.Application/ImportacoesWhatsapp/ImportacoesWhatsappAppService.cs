using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.ImportacoesWhatsapp;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.ImportacoesWhatsapp;

public sealed class ImportacoesWhatsappAppService(
    IAppDbContext dbContext,
    IFileStorage fileStorage,
    IDocumentExtractor documentExtractor,
    IImportSuggestionService importSuggestionService)
{
    private static readonly string[] MimeTypesSuportados =
    [
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/webp",
        "text/plain"
    ];

    public async Task<PagedResult<ImportacaoWhatsappResumoResponse>> ListarAsync(
        ImportacaoWhatsappListQueryRequest query,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.ImportacoesWhatsapp.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = query.Search.Trim().ToLowerInvariant();
            consulta = consulta.Where(x =>
                x.Remetente.ToLower().Contains(termo) ||
                (x.TextoBruto != null && x.TextoBruto.ToLower().Contains(termo)) ||
                (x.NomeArquivo != null && x.NomeArquivo.ToLower().Contains(termo)));
        }

        if (!string.IsNullOrWhiteSpace(query.StatusCodigo))
        {
            var status = NormalizarStatus(query.StatusCodigo);
            consulta = consulta.Where(x => x.Status == status);
        }

        consulta = query.SortDirection == SortDirection.Asc
            ? consulta.OrderBy(x => x.RecebidoEmUtc)
            : consulta.OrderByDescending(x => x.RecebidoEmUtc);

        var totalItems = await consulta.CountAsync(cancellationToken);

        var items = await consulta
            .Select(x => new ImportacaoWhatsappResumoResponse(
                x.Id,
                MapearTipoOrigemCodigo(x.TipoOrigem),
                MapearTipoOrigemNome(x.TipoOrigem),
                x.Remetente,
                x.TextoBruto,
                x.NomeArquivo,
                x.MimeType,
                MapearStatusCodigo(x.Status),
                MapearStatusNome(x.Status),
                x.ConfiancaExtracao,
                x.Itens.Count,
                x.Itens.Count(item => item.Status == StatusItemImportadoWhatsapp.Sugerido),
                x.RecebidoEmUtc,
                x.ProcessadoEmUtc))
            .ApplyPagination(query)
            .ToArrayAsync(cancellationToken);

        return PagedResult<ImportacaoWhatsappResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);
    }

    public async Task<ImportacaoWhatsappDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var importacao = await CarregarImportacaoAsync(id, cancellationToken);
        return importacao is null ? null : MapearDetalhe(importacao);
    }

    public async Task<ImportacaoWhatsappDetalheResponse> ReceberWebhookAsync(
        ReceberImportacaoWhatsappWebhookRequest request,
        CancellationToken cancellationToken)
    {
        ValidarWebhook(request);

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

        return MapearDetalhe(importacao);
    }

    public async Task<ImportacaoWhatsappDetalheResponse?> ReprocessarAsync(Guid id, CancellationToken cancellationToken)
    {
        var existeImportacao = await dbContext.ImportacoesWhatsapp
            .AsNoTracking()
            .AnyAsync(x => x.Id == id, cancellationToken);

        if (!existeImportacao)
        {
            return null;
        }

        await dbContext.ItensImportadosWhatsapp
            .Where(x => x.ImportacaoWhatsappId == id)
            .ExecuteDeleteAsync(cancellationToken);

        var importacao = await CarregarImportacaoAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Importacao nao encontrada apos limpar o contexto.");

        await ProcessarImportacaoAsync(importacao, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapearDetalhe(importacao);
    }

    public async Task<ImportacaoWhatsappDetalheResponse?> ConfirmarItemAsync(
        Guid itemId,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken)
    {
        var importacao = await CarregarImportacaoPorItemAsync(itemId, cancellationToken);
        if (importacao is null)
        {
            return null;
        }

        var item = importacao.Itens.Single(x => x.Id == itemId);
        try
        {
            item.Confirmar(request.Observacao);
            importacao.AtualizarStatusRevisao();
        }
        catch (InvalidOperationException exception)
        {
            throw ValidationExceptionFactory.Create("Status", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapearDetalhe(importacao);
    }

    public async Task<ImportacaoWhatsappDetalheResponse?> RejeitarItemAsync(
        Guid itemId,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken)
    {
        var importacao = await CarregarImportacaoPorItemAsync(itemId, cancellationToken);
        if (importacao is null)
        {
            return null;
        }

        var item = importacao.Itens.Single(x => x.Id == itemId);
        try
        {
            item.Rejeitar(request.Observacao);
            importacao.AtualizarStatusRevisao();
        }
        catch (InvalidOperationException exception)
        {
            throw ValidationExceptionFactory.Create("Status", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapearDetalhe(importacao);
    }

    private async Task ProcessarImportacaoAsync(ImportacaoWhatsapp importacao, CancellationToken cancellationToken)
    {
        importacao.MarcarEmProcessamento();

        var extractionResult = await documentExtractor.ExtractAsync(
            new DocumentExtractionRequest(
                importacao.TextoBruto,
                importacao.NomeArquivo,
                importacao.MimeType,
                importacao.CaminhoArquivo),
            cancellationToken);

        if (!extractionResult.Success || string.IsNullOrWhiteSpace(extractionResult.TextoExtraido))
        {
            importacao.RegistrarErroExtracao(extractionResult.MensagemErro ?? "Nao foi possivel extrair conteudo da importacao.");
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
            .Select(item => ItemImportadoWhatsapp.Criar(
                importacao.Id,
                item.TipoSugestao,
                item.PayloadSugeridoJson))
            .ToArray();

        importacao.SubstituirItens(itensGerados);
        dbContext.ItensImportadosWhatsapp.AddRange(itensGerados);
    }

    private static void ValidarWebhook(ReceberImportacaoWhatsappWebhookRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Remetente))
        {
            throw ValidationExceptionFactory.Create("Remetente", "Remetente e obrigatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.TextoBruto) && string.IsNullOrWhiteSpace(request.ArquivoBase64))
        {
            throw ValidationExceptionFactory.Create("TextoBruto", "Informe texto bruto ou arquivo para processar a importacao.");
        }

        if (!string.IsNullOrWhiteSpace(request.ArquivoBase64))
        {
            if (string.IsNullOrWhiteSpace(request.NomeArquivo))
            {
                throw ValidationExceptionFactory.Create("NomeArquivo", "Nome do arquivo e obrigatorio quando houver artefato.");
            }

            if (string.IsNullOrWhiteSpace(request.MimeType))
            {
                throw ValidationExceptionFactory.Create("MimeType", "Mime type e obrigatorio quando houver artefato.");
            }

            if (!MimeTypesSuportados.Contains(request.MimeType.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                throw ValidationExceptionFactory.Create("MimeType", "Mime type nao suportado para importacao.");
            }
        }
    }

    private async Task<ImportacaoWhatsapp?> CarregarImportacaoAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.ImportacoesWhatsapp
            .Include(x => x.Itens)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    private async Task<ImportacaoWhatsapp?> CarregarImportacaoPorItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var importacaoId = await dbContext.ItensImportadosWhatsapp
            .Where(x => x.Id == itemId)
            .Select(x => x.ImportacaoWhatsappId)
            .SingleOrDefaultAsync(cancellationToken);

        return importacaoId == Guid.Empty
            ? null
            : await CarregarImportacaoAsync(importacaoId, cancellationToken);
    }

    private static ImportacaoWhatsappDetalheResponse MapearDetalhe(ImportacaoWhatsapp importacao)
    {
        return new ImportacaoWhatsappDetalheResponse(
            importacao.Id,
            MapearTipoOrigemCodigo(importacao.TipoOrigem),
            MapearTipoOrigemNome(importacao.TipoOrigem),
            importacao.Remetente,
            importacao.TextoBruto,
            importacao.NomeArquivo,
            importacao.CaminhoArquivo,
            importacao.MimeType,
            MapearStatusCodigo(importacao.Status),
            MapearStatusNome(importacao.Status),
            importacao.ConfiancaExtracao,
            importacao.MensagemErro,
            importacao.RecebidoEmUtc,
            importacao.ProcessadoEmUtc,
            importacao.ConfirmadoEmUtc,
            importacao.RejeitadoEmUtc,
            importacao.Itens
                .OrderBy(item => item.CreatedAtUtc)
                .Select(item => new ItemImportadoWhatsappResponse(
                    item.Id,
                    item.ImportacaoWhatsappId,
                    MapearTipoSugestaoCodigo(item.TipoSugestao),
                    MapearTipoSugestaoNome(item.TipoSugestao),
                    item.PayloadSugeridoJson,
                    MapearStatusItemCodigo(item.Status),
                    MapearStatusItemNome(item.Status),
                    item.Observacao,
                    item.ConfirmadoEmUtc,
                    item.RejeitadoEmUtc))
                .ToArray());
    }

    private static TipoOrigemImportacaoWhatsapp MapearTipoOrigem(TipoOrigemImportacaoWhatsappRequest tipoOrigem)
    {
        return tipoOrigem switch
        {
            TipoOrigemImportacaoWhatsappRequest.Texto => TipoOrigemImportacaoWhatsapp.Texto,
            TipoOrigemImportacaoWhatsappRequest.Imagem => TipoOrigemImportacaoWhatsapp.Imagem,
            TipoOrigemImportacaoWhatsappRequest.Pdf => TipoOrigemImportacaoWhatsapp.Pdf,
            TipoOrigemImportacaoWhatsappRequest.Arquivo => TipoOrigemImportacaoWhatsapp.Arquivo,
            _ => throw new ApplicationValidationException("Um ou mais campos sao invalidos.", new Dictionary<string, string[]>
            {
                ["TipoOrigem"] = ["Tipo de origem invalido."]
            })
        };
    }

    private static StatusImportacaoWhatsapp NormalizarStatus(string statusCodigo)
    {
        return statusCodigo.Trim().ToUpperInvariant() switch
        {
            "RECEBIDO" => StatusImportacaoWhatsapp.Recebido,
            "EM_PROCESSAMENTO" => StatusImportacaoWhatsapp.EmProcessamento,
            "EXTRAIDO_COM_SUCESSO" => StatusImportacaoWhatsapp.ExtraidoComSucesso,
            "PENDENTE_REVISAO" => StatusImportacaoWhatsapp.PendenteRevisao,
            "CONFIRMADO" => StatusImportacaoWhatsapp.Confirmado,
            "REJEITADO" => StatusImportacaoWhatsapp.Rejeitado,
            "ERRO_EXTRACAO" => StatusImportacaoWhatsapp.ErroExtracao,
            _ => throw ValidationExceptionFactory.Create("StatusCodigo", "Status de importacao invalido.")
        };
    }

    private static string MapearTipoOrigemCodigo(TipoOrigemImportacaoWhatsapp tipoOrigem)
    {
        return tipoOrigem switch
        {
            TipoOrigemImportacaoWhatsapp.Texto => "TEXTO",
            TipoOrigemImportacaoWhatsapp.Imagem => "IMAGEM",
            TipoOrigemImportacaoWhatsapp.Pdf => "PDF",
            TipoOrigemImportacaoWhatsapp.Arquivo => "ARQUIVO",
            _ => throw new ArgumentOutOfRangeException(nameof(tipoOrigem))
        };
    }

    private static string MapearTipoOrigemNome(TipoOrigemImportacaoWhatsapp tipoOrigem)
    {
        return tipoOrigem switch
        {
            TipoOrigemImportacaoWhatsapp.Texto => "Texto",
            TipoOrigemImportacaoWhatsapp.Imagem => "Imagem",
            TipoOrigemImportacaoWhatsapp.Pdf => "PDF",
            TipoOrigemImportacaoWhatsapp.Arquivo => "Arquivo",
            _ => throw new ArgumentOutOfRangeException(nameof(tipoOrigem))
        };
    }

    private static string MapearStatusCodigo(StatusImportacaoWhatsapp status)
    {
        return status switch
        {
            StatusImportacaoWhatsapp.Recebido => "RECEBIDO",
            StatusImportacaoWhatsapp.EmProcessamento => "EM_PROCESSAMENTO",
            StatusImportacaoWhatsapp.ExtraidoComSucesso => "EXTRAIDO_COM_SUCESSO",
            StatusImportacaoWhatsapp.PendenteRevisao => "PENDENTE_REVISAO",
            StatusImportacaoWhatsapp.Confirmado => "CONFIRMADO",
            StatusImportacaoWhatsapp.Rejeitado => "REJEITADO",
            StatusImportacaoWhatsapp.ErroExtracao => "ERRO_EXTRACAO",
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }

    private static string MapearStatusNome(StatusImportacaoWhatsapp status)
    {
        return status switch
        {
            StatusImportacaoWhatsapp.Recebido => "Recebido",
            StatusImportacaoWhatsapp.EmProcessamento => "Em processamento",
            StatusImportacaoWhatsapp.ExtraidoComSucesso => "Extraido com sucesso",
            StatusImportacaoWhatsapp.PendenteRevisao => "Pendente revisao",
            StatusImportacaoWhatsapp.Confirmado => "Confirmado",
            StatusImportacaoWhatsapp.Rejeitado => "Rejeitado",
            StatusImportacaoWhatsapp.ErroExtracao => "Erro de extracao",
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }

    private static string MapearTipoSugestaoCodigo(TipoSugestaoImportacaoWhatsapp tipoSugestao)
    {
        return tipoSugestao switch
        {
            TipoSugestaoImportacaoWhatsapp.ContaPagar => "CONTA_PAGAR",
            TipoSugestaoImportacaoWhatsapp.ContaReceber => "CONTA_RECEBER",
            TipoSugestaoImportacaoWhatsapp.CompraCartao => "COMPRA_CARTAO",
            TipoSugestaoImportacaoWhatsapp.Movimentacao => "MOVIMENTACAO",
            TipoSugestaoImportacaoWhatsapp.ItemExtrato => "ITEM_EXTRATO",
            _ => throw new ArgumentOutOfRangeException(nameof(tipoSugestao))
        };
    }

    private static string MapearTipoSugestaoNome(TipoSugestaoImportacaoWhatsapp tipoSugestao)
    {
        return tipoSugestao switch
        {
            TipoSugestaoImportacaoWhatsapp.ContaPagar => "Conta a pagar",
            TipoSugestaoImportacaoWhatsapp.ContaReceber => "Conta a receber",
            TipoSugestaoImportacaoWhatsapp.CompraCartao => "Compra em cartao",
            TipoSugestaoImportacaoWhatsapp.Movimentacao => "Movimentacao",
            TipoSugestaoImportacaoWhatsapp.ItemExtrato => "Item de extrato",
            _ => throw new ArgumentOutOfRangeException(nameof(tipoSugestao))
        };
    }

    private static string MapearStatusItemCodigo(StatusItemImportadoWhatsapp status)
    {
        return status switch
        {
            StatusItemImportadoWhatsapp.Sugerido => "SUGERIDO",
            StatusItemImportadoWhatsapp.Confirmado => "CONFIRMADO",
            StatusItemImportadoWhatsapp.Rejeitado => "REJEITADO",
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }

    private static string MapearStatusItemNome(StatusItemImportadoWhatsapp status)
    {
        return status switch
        {
            StatusItemImportadoWhatsapp.Sugerido => "Sugerido",
            StatusItemImportadoWhatsapp.Confirmado => "Confirmado",
            StatusItemImportadoWhatsapp.Rejeitado => "Rejeitado",
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }
}
