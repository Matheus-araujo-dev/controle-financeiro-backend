using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Application.Financeiro.ContasPagar;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;
using ControleFinanceiro.Contracts.PlanejamentoCompras;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.PlanejamentoCompras;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.PlanejamentoCompras;

public sealed class PlanejamentoCompraAppService(IAppDbContext dbContext, IContaPagarCommandService contaPagarCommands)
{
    public async Task<CompraPlanejadaListResponse> ListarAsync(
        CompraPlanejadaListQueryRequest query,
        CancellationToken cancellationToken)
    {
        var consulta =
            from compra in dbContext.ComprasPlanejadas.AsNoTracking()
            join contaGerencial in dbContext.ContasGerenciais.AsNoTracking() on compra.ContaGerencialId equals contaGerencial.Id into contasGerenciaisJoin
            from contaGerencial in contasGerenciaisJoin.DefaultIfEmpty()
            join responsavel in dbContext.Pessoas.AsNoTracking() on compra.ResponsavelId equals responsavel.Id into responsaveisJoin
            from responsavel in responsaveisJoin.DefaultIfEmpty()
            join contaPagar in dbContext.ContasPagar.AsNoTracking() on compra.ContaPagarGeradaId equals contaPagar.Id into contasPagarJoin
            from contaPagar in contasPagarJoin.DefaultIfEmpty()
            select new
            {
                compra.Id,
                compra.Titulo,
                compra.ValorEstimado,
                compra.DataDesejada,
                compra.Prioridade,
                compra.Status,
                compra.Parcelavel,
                compra.QuantidadeParcelasDesejada,
                compra.ContaGerencialId,
                ContaGerencialDescricao = contaGerencial != null ? contaGerencial.Descricao : "Conta gerencial indisponível",
                compra.ResponsavelId,
                ResponsavelNome = responsavel != null ? responsavel.Nome : "Responsável indisponível",
                compra.Link,
                ContaPagarGeradaId = contaPagar != null && !contaPagar.CartaoId.HasValue ? compra.ContaPagarGeradaId : null,
                ConvertidaEmContaPagarEmUtc = contaPagar != null && !contaPagar.CartaoId.HasValue ? compra.ConvertidaEmContaPagarEmUtc : null
            };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = $"%{query.Search.Trim()}%";
            consulta = consulta.Where(x =>
                EF.Functions.Like(x.Titulo, termo) ||
                EF.Functions.Like(x.ContaGerencialDescricao, termo) ||
                EF.Functions.Like(x.ResponsavelNome, termo));
        }

        if (!string.IsNullOrWhiteSpace(query.Prioridade))
        {
            var prioridade = ParsePrioridade(query.Prioridade);
            consulta = consulta.Where(x => x.Prioridade == prioridade);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = ParseStatus(query.Status);
            consulta = consulta.Where(x => x.Status == status);
        }

        if (query.ResponsavelId.HasValue)
        {
            consulta = consulta.Where(x => x.ResponsavelId == query.ResponsavelId.Value);
        }

        consulta = query.SortDirection == SortDirection.Desc
            ? consulta.OrderByDescending(x => x.DataDesejada).ThenByDescending(x => x.Titulo)
            : consulta.OrderBy(x => x.DataDesejada).ThenBy(x => x.Titulo);

        var totalItems = await consulta.CountAsync(cancellationToken);
        var valorTotalEstimado = await consulta.SumAsync(x => (decimal?)x.ValorEstimado, cancellationToken) ?? 0m;
        var items = await consulta
            .ApplyPagination(query)
            .Select(x => new CompraPlanejadaResumoResponse(
                x.Id,
                x.Titulo,
                x.ValorEstimado,
                x.DataDesejada,
                x.Prioridade.ToString(),
                x.Status.ToString(),
                x.Parcelavel,
                x.QuantidadeParcelasDesejada,
                x.ContaGerencialId,
                x.ContaGerencialDescricao,
                x.ResponsavelId,
                x.ResponsavelNome,
                x.Link,
                x.ContaPagarGeradaId,
                x.ConvertidaEmContaPagarEmUtc))
            .ToListAsync(cancellationToken);

        var paged = PagedResult<CompraPlanejadaResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);
        return new CompraPlanejadaListResponse(
            paged.Items,
            paged.Page,
            paged.PageSize,
            paged.TotalItems,
            paged.TotalPages,
            new CompraPlanejadaListSummaryResponse(
                totalItems,
                decimal.Round(valorTotalEstimado, 2, MidpointRounding.AwayFromZero)));
    }

    public async Task<CompraPlanejadaDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await (
            from compra in dbContext.ComprasPlanejadas.AsNoTracking()
            join contaGerencial in dbContext.ContasGerenciais.AsNoTracking() on compra.ContaGerencialId equals contaGerencial.Id into contasGerenciaisJoin
            from contaGerencial in contasGerenciaisJoin.DefaultIfEmpty()
            join responsavel in dbContext.Pessoas.AsNoTracking() on compra.ResponsavelId equals responsavel.Id into responsaveisJoin
            from responsavel in responsaveisJoin.DefaultIfEmpty()
            join contaPagar in dbContext.ContasPagar.AsNoTracking() on compra.ContaPagarGeradaId equals contaPagar.Id into contasPagarJoin
            from contaPagar in contasPagarJoin.DefaultIfEmpty()
            where compra.Id == id
            select new CompraPlanejadaDetalheResponse(
                compra.Id,
                compra.Titulo,
                compra.Descricao,
                compra.ValorEstimado,
                compra.DataDesejada,
                compra.Prioridade.ToString(),
                compra.Status.ToString(),
                compra.Parcelavel,
                compra.QuantidadeParcelasDesejada,
                compra.ContaGerencialId,
                contaGerencial != null ? contaGerencial.Descricao : "Conta gerencial indisponível",
                compra.ResponsavelId,
                responsavel != null ? responsavel.Nome : "Responsável indisponível",
                compra.Link,
                compra.Observacao,
                contaPagar != null && !contaPagar.CartaoId.HasValue ? compra.ContaPagarGeradaId : null,
                contaPagar != null && !contaPagar.CartaoId.HasValue ? compra.ConvertidaEmContaPagarEmUtc : null,
                compra.CreatedAtUtc,
                compra.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CompraPlanejadaDetalheResponse> CriarAsync(
        CriarCompraPlanejadaRequest request,
        CancellationToken cancellationToken)
    {
        await ValidarReferenciasAsync(request.ContaGerencialId, request.ResponsavelId, cancellationToken);

        PlanejamentoCompra compra;

        try
        {
            compra = PlanejamentoCompra.Criar(
                request.Titulo,
                request.Descricao,
                request.ValorEstimado,
                request.DataDesejada,
                ParsePrioridade(request.Prioridade),
                ParseStatus(request.Status),
                request.Parcelavel,
                request.QuantidadeParcelasDesejada,
                request.ContaGerencialId,
                request.ResponsavelId,
                request.Link,
                request.Observacao);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            throw ConverterParaValidacao(exception);
        }

        dbContext.ComprasPlanejadas.Add(compra);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(compra.Id, cancellationToken)
            ?? throw new InvalidOperationException("Compra planejada criada não foi encontrada.");
    }

    public async Task<CompraPlanejadaDetalheResponse?> AtualizarAsync(
        Guid id,
        AtualizarCompraPlanejadaRequest request,
        CancellationToken cancellationToken)
    {
        var compra = await dbContext.ComprasPlanejadas.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (compra is null)
        {
            return null;
        }

        if (compra.Status == StatusPlanejamentoCompra.Comprada || compra.ContaPagarGeradaId.HasValue)
        {
            throw ValidationExceptionFactory.Create("Status", "Compra planejada já realizada não pode ser editada.");
        }

        await ValidarReferenciasAsync(request.ContaGerencialId, request.ResponsavelId, cancellationToken);

        try
        {
            compra.Atualizar(
                request.Titulo,
                request.Descricao,
                request.ValorEstimado,
                request.DataDesejada,
                ParsePrioridade(request.Prioridade),
                ParseStatus(request.Status),
                request.Parcelavel,
                request.QuantidadeParcelasDesejada,
                request.ContaGerencialId,
                request.ResponsavelId,
                request.Link,
                request.Observacao);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            throw ConverterParaValidacao(exception);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await ObterPorIdAsync(id, cancellationToken);
    }

    public async Task<CompraPlanejadaDetalheResponse?> RealizarAsync(
        Guid id,
        RealizarCompraPlanejadaRequest request,
        CancellationToken cancellationToken)
    {
        var compra = await dbContext.ComprasPlanejadas.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (compra is null)
        {
            return null;
        }

        if (compra.Status != StatusPlanejamentoCompra.Planejada || compra.ContaPagarGeradaId.HasValue)
        {
            throw ValidationExceptionFactory.Create("Status", "Compra planejada já foi realizada ou cancelada.");
        }

        if (request.QuantidadeParcelas < 1)
        {
            throw ValidationExceptionFactory.Create("QuantidadeParcelas", "Quantidade de parcelas deve ser maior que zero.");
        }

        if (request.QuantidadeParcelas > 1 && !compra.Parcelavel)
        {
            throw ValidationExceptionFactory.Create("QuantidadeParcelas", "Esta compra planejada não está marcada como parcelável.");
        }

        var formaPagamento = await dbContext.FormasPagamento
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.FormaPagamentoId, cancellationToken);

        if (formaPagamento is null)
        {
            throw ValidationExceptionFactory.Create("FormaPagamentoId", "Forma de pagamento não encontrada.");
        }

        if (!formaPagamento.EhCartao && !request.DataVencimento.HasValue && !formaPagamento.BaixarAutomaticamente)
        {
            throw ValidationExceptionFactory.Create("DataVencimento", "Informe a data de vencimento para formas sem baixa automática.");
        }

        await contaPagarCommands.CriarAsync(
            new CriarContaPagarRequest(
                compra.Id,
                request.NumeroDocumento,
                request.DataCompra,
                compra.ResponsavelId,
                request.RecebedorId,
                request.DataVencimento ?? request.DataCompra,
                request.FormaPagamentoId,
                request.CartaoId,
                request.ContaBancariaId,
                formaPagamento.BaixarAutomaticamente && !formaPagamento.EhCartao ? request.DataCompra : null,
                compra.ValorEstimado,
                0m,
                0m,
                0m,
                request.QuantidadeParcelas,
                string.IsNullOrWhiteSpace(request.Descricao) ? compra.Titulo : request.Descricao.Trim(),
                string.IsNullOrWhiteSpace(request.Observacao) ? compra.Observacao : request.Observacao.Trim(),
                [new RateioRequest(compra.ContaGerencialId, compra.ValorEstimado)],
                null),
            cancellationToken);

        return await ObterPorIdAsync(id, cancellationToken);
    }

    private async Task ValidarReferenciasAsync(Guid contaGerencialId, Guid responsavelId, CancellationToken cancellationToken)
    {
        await ContaGerencialLancamentoValidator.ValidarContaLancavelPorTipoAsync(
            dbContext,
            contaGerencialId,
            TipoContaGerencial.Despesa,
            "ContaGerencialId",
            "Conta gerencial não encontrada.",
            "Somente contas gerenciais filhas podem ser usadas no planejador de compras.",
            "Compras planejadas aceitam apenas contas gerenciais de despesa.",
            cancellationToken);

        if (!await dbContext.Pessoas.AnyAsync(x => x.Id == responsavelId, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("ResponsavelId", "Responsável não encontrado.");
        }
    }

    private static PrioridadePlanejamentoCompra ParsePrioridade(string prioridade)
    {
        if (Enum.TryParse<PrioridadePlanejamentoCompra>(prioridade, true, out var result))
        {
            return result;
        }

        throw ValidationExceptionFactory.Create("Prioridade", "Prioridade inválida.");
    }

    private static StatusPlanejamentoCompra ParseStatus(string status)
    {
        if (Enum.TryParse<StatusPlanejamentoCompra>(status, true, out var result))
        {
            return result;
        }

        throw ValidationExceptionFactory.Create("Status", "Status inválido.");
    }

    private static Exception ConverterParaValidacao(Exception exception)
    {
        var campo = exception switch
        {
            ArgumentException { ParamName: "titulo" } => "Titulo",
            ArgumentException { ParamName: "contaGerencialId" } => "ContaGerencialId",
            ArgumentException { ParamName: "responsavelId" } => "ResponsavelId",
            ArgumentException { ParamName: "link" } => "Link",
            ArgumentOutOfRangeException { ParamName: "valorEstimado" } => "ValorEstimado",
            ArgumentOutOfRangeException { ParamName: "quantidadeParcelasDesejada" } => "QuantidadeParcelasDesejada",
            _ => "Request"
        };

        return ValidationExceptionFactory.Create(campo, exception.Message);
    }
}
