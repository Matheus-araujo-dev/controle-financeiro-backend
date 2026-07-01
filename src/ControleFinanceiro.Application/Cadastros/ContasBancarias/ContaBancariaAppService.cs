using ControleFinanceiro.Application.Common.Extensions;
using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Cadastros.ContasBancarias;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Cadastros.ContasBancarias;

public sealed class ContaBancariaAppService(IAppDbContext dbContext)
{
    public async Task<ContaBancariaListResponse> ListarAsync(
        ContaBancariaListQueryRequest query,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.ContasBancarias.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = $"%{query.Search.Trim().ToLower()}%";
            consulta = consulta.Where(x =>
                EF.Functions.Like(x.Nome.ToLower(), termo) ||
                EF.Functions.Like(x.Banco.ToLower(), termo) ||
                (x.NumeroConta != null && EF.Functions.Like(x.NumeroConta.ToLower(), termo)));
        }

        if (!string.IsNullOrWhiteSpace(query.Banco))
        {
            var banco = $"%{query.Banco.Trim().ToLower()}%";
            consulta = consulta.Where(x => EF.Functions.Like(x.Banco.ToLower(), banco));
        }

        if (!string.IsNullOrWhiteSpace(query.Agencia))
        {
            var agencia = $"%{query.Agencia.Trim().ToLower()}%";
            consulta = consulta.Where(x => x.Agencia != null && EF.Functions.Like(x.Agencia.ToLower(), agencia));
        }

        if (!string.IsNullOrWhiteSpace(query.NumeroConta))
        {
            var numeroConta = $"%{query.NumeroConta.Trim().ToLower()}%";
            consulta = consulta.Where(x => x.NumeroConta != null && EF.Functions.Like(x.NumeroConta.ToLower(), numeroConta));
        }

        if (!string.IsNullOrWhiteSpace(query.TipoConta))
        {
            var tipoConta = $"%{query.TipoConta.Trim().ToLower()}%";
            consulta = consulta.Where(x => x.TipoConta != null && EF.Functions.Like(x.TipoConta.ToLower(), tipoConta));
        }

        if (query.Ativo.HasValue)
        {
            consulta = consulta.Where(x => x.Ativo == query.Ativo.Value);
        }

        if (query.TiposConta is { Count: > 0 })
        {
            var tipos = query.TiposConta;
            consulta = consulta.Where(x => x.TipoConta != null && tipos.Contains(x.TipoConta));
        }

        // Base para os totalizadores: considera todo o conjunto filtrado antes da paginação.
        var baseResumo = await consulta
            .Select(x => new { x.Id, x.SaldoInicial, x.LimiteCartoesCompartilhado, x.Ativo })
            .ToListAsync(cancellationToken);

        var totalItems = baseResumo.Count;
        var todosIds = baseResumo.Select(x => x.Id).ToArray();
        var comprometimentoTotal = await CarregarComprometimentoPorContaAsync(todosIds, cancellationToken);
        var movimentadoTotal = await CarregarMovimentadoPorContaAsync(todosIds, cancellationToken);

        var saldoTotal = baseResumo.Sum(x => CalcularSaldoAtual(x.SaldoInicial, movimentadoTotal.GetValueOrDefault(x.Id)));
        var creditoDisponivel = baseResumo.Sum(x =>
            (x.LimiteCartoesCompartilhado ?? 0m) - comprometimentoTotal.GetValueOrDefault(x.Id));
        var summary = new ContaBancariaListSummaryResponse(
            totalItems,
            baseResumo.Count(x => x.Ativo),
            decimal.Round(saldoTotal, 2, MidpointRounding.AwayFromZero),
            decimal.Round(creditoDisponivel, 2, MidpointRounding.AwayFromZero));

        consulta = (query.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "banco" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Banco).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.Banco).ThenBy(x => x.Nome),
            "agencia" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Agencia).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.Agencia).ThenBy(x => x.Nome),
            "numeroconta" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.NumeroConta).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.NumeroConta).ThenBy(x => x.Nome),
            "tipoconta" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.TipoConta).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.TipoConta).ThenBy(x => x.Nome),
            "saldoinicial" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.SaldoInicial).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.SaldoInicial).ThenBy(x => x.Nome),
            "ativo" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Ativo).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.Ativo).ThenBy(x => x.Nome),
            _ => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.Nome)
        };

        var selecionadas = await consulta.ApplyPagination(query)
            .Select(x => new ContaBancariaProjection(
                x.Id,
                x.Nome,
                x.Banco,
                x.Agencia,
                x.NumeroConta,
                x.TipoConta,
                x.SaldoInicial,
                x.DataSaldoInicial,
                x.LimiteCartoesCompartilhado,
                x.Ativo,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        var items = selecionadas
            .Select(x =>
            {
                var valorComprometido = comprometimentoTotal.GetValueOrDefault(x.Id);
                return new ContaBancariaResumoResponse(
                    x.Id,
                    x.Nome,
                    x.Banco,
                    x.Agencia,
                    x.NumeroConta,
                    x.TipoConta,
                    x.SaldoInicial,
                    x.DataSaldoInicial,
                    CalcularSaldoAtual(x.SaldoInicial, movimentadoTotal.GetValueOrDefault(x.Id)),
                    x.LimiteCartoesCompartilhado,
                    valorComprometido,
                    CalcularDisponivel(x.LimiteCartoesCompartilhado, valorComprometido),
                    x.Ativo);
            })
            .ToList();

        var paged = PagedResult<ContaBancariaResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);

        return new ContaBancariaListResponse(
            paged.Items,
            paged.Page,
            paged.PageSize,
            paged.TotalItems,
            paged.TotalPages,
            summary);
    }

    public async Task<ContaBancariaDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasBancarias.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new ContaBancariaProjection(
                x.Id,
                x.Nome,
                x.Banco,
                x.Agencia,
                x.NumeroConta,
                x.TipoConta,
                x.SaldoInicial,
                x.DataSaldoInicial,
                x.LimiteCartoesCompartilhado,
                x.Ativo,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

        if (conta is null)
        {
            return null;
        }

        var comprometimento = await CarregarComprometimentoPorContaAsync([id], cancellationToken);
        var valorComprometido = comprometimento.GetValueOrDefault(id);
        var movimentadoPorConta = await CarregarMovimentadoPorContaAsync([id], cancellationToken);

        return new ContaBancariaDetalheResponse(
            conta.Id,
            conta.Nome,
            conta.Banco,
            conta.Agencia,
            conta.NumeroConta,
            conta.TipoConta,
            conta.SaldoInicial,
            conta.DataSaldoInicial,
            CalcularSaldoAtual(conta.SaldoInicial, movimentadoPorConta.GetValueOrDefault(id)),
            conta.LimiteCartoesCompartilhado,
            valorComprometido,
            CalcularDisponivel(conta.LimiteCartoesCompartilhado, valorComprometido),
            conta.Ativo,
            conta.CreatedAtUtc,
            conta.UpdatedAtUtc);
    }

    public async Task<ContaBancariaDetalheResponse> CriarAsync(
        CriarContaBancariaRequest request,
        CancellationToken cancellationToken)
    {
        ContaBancaria conta;

        try
        {
            conta = ContaBancaria.Criar(
                request.Nome,
                request.Banco,
                request.Agencia,
                request.NumeroConta,
                request.TipoConta,
                request.SaldoInicial,
                request.DataSaldoInicial,
                request.LimiteCartoesCompartilhado,
                request.Ativo);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            throw ConverterParaValidacao(exception);
        }

        dbContext.ContasBancarias.Add(conta);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(conta.Id, cancellationToken)
            ?? throw new InvalidOperationException("Conta bancária criada não foi encontrada.");
    }

    public async Task<ContaBancariaDetalheResponse?> AtualizarAsync(
        Guid id,
        AtualizarContaBancariaRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasBancarias.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        try
        {
            conta.Atualizar(
                request.Nome,
                request.Banco,
                request.Agencia,
                request.NumeroConta,
                request.TipoConta,
                request.SaldoInicial,
                request.DataSaldoInicial,
                request.LimiteCartoesCompartilhado,
                request.Ativo);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            throw ConverterParaValidacao(exception);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(id, cancellationToken);
    }

    private async Task<Dictionary<Guid, decimal>> CarregarComprometimentoPorContaAsync(
        IReadOnlyCollection<Guid> contaIds,
        CancellationToken cancellationToken)
    {
        if (contaIds.Count == 0)
        {
            return [];
        }

        return await (
            from conta in dbContext.ContasPagar.AsNoTracking()
            join cartao in dbContext.Cartoes.AsNoTracking() on conta.CartaoId equals cartao.Id
            where cartao.ContaBancariaPagamentoPadraoId.HasValue
                  && conta.StatusContaId != StatusConta.CanceladaId
                  && conta.StatusContaId != StatusConta.LiquidadaId
            where contaIds.Contains(cartao.ContaBancariaPagamentoPadraoId!.Value)
            group conta by cartao.ContaBancariaPagamentoPadraoId!.Value into groupByConta
            select new
            {
                ContaBancariaId = groupByConta.Key,
                Valor = groupByConta.Sum(x => x.ValorLiquido)
            })
            .ToDictionaryAsync(x => x.ContaBancariaId, x => x.Valor, cancellationToken);
    }

    /// <summary>
    /// Saldo movimentado por conta: entradas - saídas das movimentações Realizadas não canceladas,
    /// a partir da data do saldo inicial (mesma semântica do saldo do dashboard, por conta).
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> CarregarMovimentadoPorContaAsync(
        IReadOnlyCollection<Guid> contaIds,
        CancellationToken cancellationToken)
    {
        if (contaIds.Count == 0)
        {
            return [];
        }

        return await (
            from movimento in dbContext.MovimentacoesFinanceiras.AsNoTracking()
            join conta in dbContext.ContasBancarias.AsNoTracking() on movimento.ContaBancariaId equals conta.Id
            where movimento.ContaBancariaId.HasValue
                  && contaIds.Contains(movimento.ContaBancariaId.Value)
                  && movimento.Natureza == NaturezaMovimentacao.Realizada
                  && movimento.StatusMovimentacaoId != StatusMovimentacao.CanceladaId
                  && movimento.DataMovimentacao >= conta.DataSaldoInicial
            group movimento by movimento.ContaBancariaId!.Value into porConta
            select new
            {
                ContaBancariaId = porConta.Key,
                Valor = porConta.Sum(m => m.Tipo == TipoMovimentacao.Entrada ? m.Valor : -m.Valor)
            })
            .ToDictionaryAsync(x => x.ContaBancariaId, x => x.Valor, cancellationToken);
    }

    private static decimal CalcularSaldoAtual(decimal saldoInicial, decimal movimentado) =>
        decimal.Round(saldoInicial + movimentado, 2, MidpointRounding.AwayFromZero);

    private static decimal? CalcularDisponivel(decimal? limite, decimal comprometido)
    {
        return limite.HasValue
            ? decimal.Round(limite.Value - comprometido, 2, MidpointRounding.AwayFromZero)
            : null;
    }

    private static Exception ConverterParaValidacao(Exception exception)
    {
        var campo = exception switch
        {
            ArgumentException { ParamName: "banco" } => "Banco",
            ArgumentOutOfRangeException { ParamName: "limiteCartoesCompartilhado" } => "LimiteCartoesCompartilhado",
            _ => "Nome"
        };

        return ValidationExceptionFactory.Create(campo, exception.Message);
    }

    private sealed record ContaBancariaProjection(
        Guid Id,
        string Nome,
        string Banco,
        string? Agencia,
        string? NumeroConta,
        string? TipoConta,
        decimal SaldoInicial,
        DateOnly DataSaldoInicial,
        decimal? LimiteCartoesCompartilhado,
        bool Ativo,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);
}
