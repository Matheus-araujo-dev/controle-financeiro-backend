using System.Text.Json;
using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Conciliacao;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Application.Conciliacao;

public sealed class ConciliacaoAppService(
    IAppDbContext dbContext,
    ILogger<ConciliacaoAppService> logger)
{
    public async Task<PagedResult<ConciliacaoItemResponse>> ListarAsync(
        ConciliacaoListQueryRequest query,
        CancellationToken cancellationToken)
    {
        var consulta =
            from item in dbContext.ItensImportadosWhatsapp.AsNoTracking()
            join importacao in dbContext.ImportacoesWhatsapp.AsNoTracking() on item.ImportacaoWhatsappId equals importacao.Id
            join movimento in dbContext.MovimentacoesFinanceiras.AsNoTracking() on item.MovimentacaoFinanceiraId equals movimento.Id into movimentosJoin
            from movimento in movimentosJoin.DefaultIfEmpty()
            where item.TipoSugestao == TipoSugestaoImportacaoWhatsapp.ItemExtrato &&
                  item.Status == StatusItemImportadoWhatsapp.Confirmado
            select new
            {
                item.Id,
                item.ImportacaoWhatsappId,
                importacao.Remetente,
                importacao.TextoBruto,
                importacao.NomeArquivo,
                item.PayloadSugeridoJson,
                item.MovimentacaoFinanceiraId,
                MovimentacaoConciliadaDescricao = movimento != null ? movimento.Observacao : null
            };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = query.Search.Trim().ToLowerInvariant();
            consulta = consulta.Where(x =>
                x.Remetente.ToLower().Contains(termo) ||
                (x.TextoBruto != null && x.TextoBruto.ToLower().Contains(termo)) ||
                (x.NomeArquivo != null && x.NomeArquivo.ToLower().Contains(termo)));
        }

        if (!string.IsNullOrWhiteSpace(query.StatusConciliacaoCodigo))
        {
            var status = NormalizarStatusConciliacao(query.StatusConciliacaoCodigo);
            consulta = status == "CONCILIADO"
                ? consulta.Where(x => x.MovimentacaoFinanceiraId.HasValue)
                : consulta.Where(x => !x.MovimentacaoFinanceiraId.HasValue);
        }

        consulta = query.SortDirection == SortDirection.Desc
            ? consulta.OrderByDescending(x => x.Id)
            : consulta.OrderBy(x => x.Id);

        var totalItems = await consulta.CountAsync(cancellationToken);
        var pagina = await consulta
            .ApplyPagination(query)
            .ToArrayAsync(cancellationToken);

        var candidatas = await CarregarCandidatasAsync(query.ContaBancariaId, cancellationToken);

        var items = pagina
            .Select(item =>
            {
                var payload = ExtratoPayload.Parse(item.PayloadSugeridoJson);
                var status = item.MovimentacaoFinanceiraId.HasValue
                    ? ("CONCILIADO", "Conciliado")
                    : ("PENDENTE", "Pendente");

                return new ConciliacaoItemResponse(
                    item.Id,
                    item.ImportacaoWhatsappId,
                    item.Remetente,
                    payload.Descricao ?? item.TextoBruto ?? item.NomeArquivo,
                    payload.Valor,
                    payload.Data,
                    status.Item1,
                    status.Item2,
                    item.MovimentacaoFinanceiraId,
                    item.MovimentacaoConciliadaDescricao,
                    item.MovimentacaoFinanceiraId.HasValue
                        ? []
                        : SelecionarCandidatas(payload, candidatas));
            })
            .ToArray();

        return PagedResult<ConciliacaoItemResponse>.Create(items, query.Page, query.PageSize, totalItems);
    }

    public async Task<ConciliacaoItemResponse?> ConfirmarVinculoAsync(
        Guid itemImportadoWhatsappId,
        ConfirmarVinculoConciliacaoRequest request,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.ItensImportadosWhatsapp
            .SingleOrDefaultAsync(x => x.Id == itemImportadoWhatsappId, cancellationToken);

        if (item is null)
        {
            return null;
        }

        var movimentacao = await dbContext.MovimentacoesFinanceiras
            .SingleOrDefaultAsync(x => x.Id == request.MovimentacaoFinanceiraId, cancellationToken);

        if (movimentacao is null)
        {
            throw ValidationExceptionFactory.Create("MovimentacaoFinanceiraId", "Movimentacao financeira nao encontrada.");
        }

        try
        {
            item.VincularMovimentacao(request.MovimentacaoFinanceiraId, request.Observacao);
            movimentacao.Conciliar(
                request.DataConciliacao ?? DateOnly.FromDateTime(DateTime.UtcNow),
                StatusMovimentacao.ConciliadaId,
                request.Observacao);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw ValidationExceptionFactory.Create("Conciliacao", exception.Message);
        }

        logger.LogInformation(
            "Movimentacao {MovimentacaoId} conciliada manualmente com item {ItemId}",
            movimentacao.Id,
            item.Id);

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await ListarAsync(
            new ConciliacaoListQueryRequest
            {
                Page = 1,
                PageSize = 1
            },
            cancellationToken);

        return response.Items.SingleOrDefault(x => x.ItemImportadoWhatsappId == itemImportadoWhatsappId)
            ?? await MapearItemUnicoAsync(itemImportadoWhatsappId, cancellationToken);
    }

    private async Task<ConciliacaoItemResponse?> MapearItemUnicoAsync(Guid itemImportadoWhatsappId, CancellationToken cancellationToken)
    {
        var resultado = await ListarAsync(
            new ConciliacaoListQueryRequest
            {
                Page = 1,
                PageSize = 200
            },
            cancellationToken);

        return resultado.Items.SingleOrDefault(x => x.ItemImportadoWhatsappId == itemImportadoWhatsappId);
    }

    private async Task<IReadOnlyCollection<MovimentacaoCandidataProjection>> CarregarCandidatasAsync(
        Guid? contaBancariaId,
        CancellationToken cancellationToken)
    {
        var consulta =
            from movimento in dbContext.MovimentacoesFinanceiras.AsNoTracking()
            join status in dbContext.StatusMovimentacoes.AsNoTracking() on movimento.StatusMovimentacaoId equals status.Id
            where movimento.ContaBancariaId.HasValue &&
                  movimento.Natureza == NaturezaMovimentacao.Realizada &&
                  movimento.StatusMovimentacaoId != StatusMovimentacao.ConciliadaId &&
                  movimento.StatusMovimentacaoId != StatusMovimentacao.CanceladaId &&
                  !movimento.DataConciliacao.HasValue
            select new MovimentacaoCandidataProjection(
                movimento.Id,
                movimento.DataMovimentacao,
                movimento.Tipo,
                movimento.Natureza,
                movimento.Valor,
                status.Codigo,
                movimento.Observacao,
                movimento.ContaBancariaId);

        if (contaBancariaId.HasValue)
        {
            consulta = consulta.Where(x => x.ContaBancariaId == contaBancariaId.Value);
        }

        return await consulta.ToArrayAsync(cancellationToken);
    }

    private static IReadOnlyCollection<ConciliacaoMovimentacaoCandidataResponse> SelecionarCandidatas(
        ExtratoPayload payload,
        IReadOnlyCollection<MovimentacaoCandidataProjection> candidatas)
    {
        return candidatas
            .Select(candidata =>
            {
                var score = CalcularScore(payload, candidata);
                return new
                {
                    Candidata = candidata,
                    Score = score,
                    DiferencaValor = payload.Valor.HasValue
                        ? Math.Abs(candidata.Valor - payload.Valor.Value)
                        : decimal.MaxValue,
                    DiferencaDias = payload.Data.HasValue
                        ? Math.Abs(candidata.DataMovimentacao.DayNumber - payload.Data.Value.DayNumber)
                        : int.MaxValue
                };
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.DiferencaValor)
            .ThenBy(x => x.DiferencaDias)
            .ThenByDescending(x => x.Candidata.DataMovimentacao)
            .Take(5)
            .Select(x => new ConciliacaoMovimentacaoCandidataResponse(
                x.Candidata.Id,
                x.Candidata.DataMovimentacao,
                x.Candidata.Tipo.ToString(),
                x.Candidata.Natureza.ToString(),
                x.Candidata.Valor,
                x.Candidata.StatusCodigo,
                x.Candidata.Observacao,
                x.Score))
            .ToArray();
    }

    private static int CalcularScore(ExtratoPayload payload, MovimentacaoCandidataProjection candidata)
    {
        var score = 0;

        if (payload.Valor.HasValue)
        {
            var diferencaValor = Math.Abs(candidata.Valor - payload.Valor.Value);
            score += diferencaValor switch
            {
                0m => 100,
                <= 0.50m => 60,
                <= 5m => 20,
                _ => 0
            };
        }

        if (payload.Data.HasValue)
        {
            var diferencaDias = Math.Abs(candidata.DataMovimentacao.DayNumber - payload.Data.Value.DayNumber);
            score += diferencaDias switch
            {
                0 => 30,
                <= 3 => 15,
                <= 7 => 5,
                _ => 0
            };
        }

        if (!string.IsNullOrWhiteSpace(payload.TipoMovimentacaoSugerido) &&
            string.Equals(payload.TipoMovimentacaoSugerido, candidata.Tipo.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        if (!string.IsNullOrWhiteSpace(payload.Descricao) &&
            !string.IsNullOrWhiteSpace(candidata.Observacao) &&
            candidata.Observacao.Contains(payload.Descricao, StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        return score;
    }

    private static string NormalizarStatusConciliacao(string statusConciliacaoCodigo)
    {
        return statusConciliacaoCodigo.Trim().ToUpperInvariant() switch
        {
            "PENDENTE" => "PENDENTE",
            "CONCILIADO" => "CONCILIADO",
            _ => throw ValidationExceptionFactory.Create("StatusConciliacaoCodigo", "Status de conciliacao invalido.")
        };
    }

    private sealed record MovimentacaoCandidataProjection(
        Guid Id,
        DateOnly DataMovimentacao,
        TipoMovimentacao Tipo,
        NaturezaMovimentacao Natureza,
        decimal Valor,
        string StatusCodigo,
        string? Observacao,
        Guid? ContaBancariaId);

    private sealed record ExtratoPayload(
        string? Descricao,
        decimal? Valor,
        DateOnly? Data,
        string? TipoMovimentacaoSugerido)
    {
        public static ExtratoPayload Parse(string payloadJson)
        {
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;

            var descricao = root.TryGetProperty("descricao", out var descricaoElement)
                ? descricaoElement.GetString()
                : null;

            decimal? valor = null;
            if (root.TryGetProperty("valor", out var valorElement) && valorElement.ValueKind == JsonValueKind.Number)
            {
                valor = valorElement.GetDecimal();
            }

            DateOnly? data = null;
            if (root.TryGetProperty("dataIdentificada", out var dataElement) &&
                dataElement.ValueKind == JsonValueKind.String &&
                DateOnly.TryParse(dataElement.GetString(), out var parsedDate))
            {
                data = parsedDate;
            }

            var tipoMovimentacao = root.TryGetProperty("tipoMovimentacaoSugerido", out var tipoElement)
                ? tipoElement.GetString()
                : null;

            return new ExtratoPayload(descricao, valor, data, tipoMovimentacao);
        }
    }
}
