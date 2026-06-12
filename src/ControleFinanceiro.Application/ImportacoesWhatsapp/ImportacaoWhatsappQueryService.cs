using ControleFinanceiro.Application.Common.Extensions;
using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.ImportacoesWhatsapp;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.ImportacoesWhatsapp;

public interface IImportacaoWhatsappQueryService
{
    Task<PagedResult<ImportacaoWhatsappResumoResponse>> ListarAsync(
        ImportacaoWhatsappListQueryRequest query,
        CancellationToken cancellationToken);

    Task<ImportacaoWhatsappDetalheResponse?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}

public sealed class ImportacaoWhatsappQueryService(
    IAppDbContext dbContext,
    IHeuristicPredictionService heuristicPredictionService) : IImportacaoWhatsappQueryService
{
    public async Task<PagedResult<ImportacaoWhatsappResumoResponse>> ListarAsync(
        ImportacaoWhatsappListQueryRequest query,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.ImportacoesWhatsapp.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = $"%{query.Search.Trim()}%";
            consulta = consulta.Where(x =>
                EF.Functions.Like(x.Remetente, termo) ||
                (x.TextoBruto != null && EF.Functions.Like(x.TextoBruto, termo)) ||
                (x.NomeArquivo != null && EF.Functions.Like(x.NomeArquivo, termo)));
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

    public async Task<ImportacaoWhatsappDetalheResponse?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var importacao = await CarregarImportacaoAsync(id, cancellationToken);
        return importacao is null ? null : await MapearDetalheAsync(importacao, cancellationToken);
    }

    private async Task<ImportacaoWhatsapp?> CarregarImportacaoAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.ImportacoesWhatsapp
            .Include(x => x.Itens)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    private async Task<ImportacaoWhatsappDetalheResponse> MapearDetalheAsync(
        ImportacaoWhatsapp importacao,
        CancellationToken cancellationToken)
    {
        var possuiGeracaoFinanceira = await dbContext.ContasPagar
            .AsNoTracking()
            .AnyAsync(x => x.OrigemImportacaoWhatsappId == importacao.Id, cancellationToken);

        var itensOrdenados = importacao.Itens
            .OrderBy(item => item.CreatedAtUtc)
            .ToArray();

        var predicoesPorItemId = await heuristicPredictionService.CalcularPredicoesAsync(itensOrdenados, cancellationToken);
        var statusPrevisaoPorItemId = await heuristicPredictionService.CalcularStatusPrevisaoCompraCartaoAsync(itensOrdenados, cancellationToken);

        var contaGerencialIds = itensOrdenados
            .Select(x => x.ContaGerencialId)
            .Concat(predicoesPorItemId.Values.Select(x => x.ContaGerencialId))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();

        var responsavelIds = itensOrdenados
            .Select(x => x.ResponsavelId)
            .Concat(predicoesPorItemId.Values.Select(x => x.ResponsavelId))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();

        var contasGerenciais = contaGerencialIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.ContasGerenciais
                .AsNoTracking()
                .WhereIn(x => x.Id, contaGerencialIds)
                .ToDictionaryAsync(x => x.Id, x => x.Descricao, cancellationToken);

        var responsaveis = responsavelIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Pessoas
                .AsNoTracking()
                .WhereIn(x => x.Id, responsavelIds)
                .ToDictionaryAsync(x => x.Id, x => x.Nome, cancellationToken);

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
            possuiGeracaoFinanceira,
            itensOrdenados
                .Select(item =>
                {
                    predicoesPorItemId.TryGetValue(item.Id, out var predicao);
                    statusPrevisaoPorItemId.TryGetValue(item.Id, out var statusPrevisao);

                    return new ItemImportadoWhatsappResponse(
                        item.Id,
                        item.ImportacaoWhatsappId,
                        MapearTipoSugestaoCodigo(item.TipoSugestao),
                        MapearTipoSugestaoNome(item.TipoSugestao),
                        item.PayloadSugeridoJson,
                        MapearStatusItemCodigo(item.Status),
                        MapearStatusItemNome(item.Status),
                        item.DescricaoAjustada,
                        item.MarcarComoRecorrente,
                        item.ContaGerencialId,
                        item.ContaGerencialId.HasValue && contasGerenciais.TryGetValue(item.ContaGerencialId.Value, out var contaDescricao)
                            ? contaDescricao
                            : null,
                        item.ResponsavelId,
                        item.ResponsavelId.HasValue && responsaveis.TryGetValue(item.ResponsavelId.Value, out var responsavelNome)
                            ? responsavelNome
                            : null,
                        item.ContaReceberId,
                        item.MovimentacaoFinanceiraId,
                        statusPrevisao?.StatusCodigo,
                        statusPrevisao?.StatusNome,
                        item.Observacao,
                        item.ConfirmadoEmUtc,
                        item.RejeitadoEmUtc,
                        predicao is null
                            ? null
                            : new PredicaoClassificacaoImportacaoWhatsappResponse(
                                predicao.ContaGerencialId,
                                predicao.ContaGerencialId.HasValue && contasGerenciais.TryGetValue(predicao.ContaGerencialId.Value, out var contaPredita)
                                    ? contaPredita
                                    : null,
                                predicao.ResponsavelId,
                                predicao.ResponsavelId.HasValue && responsaveis.TryGetValue(predicao.ResponsavelId.Value, out var responsavelPredito)
                                    ? responsavelPredito
                                    : null,
                                predicao.DescricaoAjustada,
                                predicao.GerarContaReceber,
                                predicao.MarcarComoRecorrente,
                                predicao.QuantidadeOcorrencias,
                                predicao.ConfiancaHistorico));
                })
                .ToArray());
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
            _ => throw ValidationExceptionFactory.Create("StatusCodigo", "Status de importação inválido.")
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
            StatusImportacaoWhatsapp.ExtraidoComSucesso => "Extraído com sucesso",
            StatusImportacaoWhatsapp.PendenteRevisao => "Pendente revisão",
            StatusImportacaoWhatsapp.Confirmado => "Confirmado",
            StatusImportacaoWhatsapp.Rejeitado => "Rejeitado",
            StatusImportacaoWhatsapp.ErroExtracao => "Erro de extração",
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
            TipoSugestaoImportacaoWhatsapp.CompraCartao => "Compra em cartão",
            TipoSugestaoImportacaoWhatsapp.Movimentacao => "Movimentação",
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
