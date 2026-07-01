using ControleFinanceiro.Application.Common.Extensions;
using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Cadastros.Cartoes;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Cadastros.Cartoes;

public sealed class CartaoAppService(IAppDbContext dbContext)
{
    public async Task<PagedResult<CartaoResumoResponse>> ListarAsync(
        CartaoListQueryRequest query,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.Cartoes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = $"%{query.Search.Trim().ToLower()}%";
            consulta = consulta.Where(x =>
                EF.Functions.Like(x.Nome.ToLower(), termo) ||
                EF.Functions.Like(x.Bandeira.ToLower(), termo) ||
                EF.Functions.Like(x.NumeroFinal.ToLower(), termo));
        }

        if (!string.IsNullOrWhiteSpace(query.Bandeira))
        {
            var bandeira = $"%{query.Bandeira.Trim().ToLower()}%";
            consulta = consulta.Where(x => EF.Functions.Like(x.Bandeira.ToLower(), bandeira));
        }

        if (!string.IsNullOrWhiteSpace(query.NumeroFinal))
        {
            var numeroFinal = $"%{query.NumeroFinal.Trim().ToLower()}%";
            consulta = consulta.Where(x => EF.Functions.Like(x.NumeroFinal.ToLower(), numeroFinal));
        }

        if (query.DiaFechamentoFatura.HasValue)
        {
            consulta = consulta.Where(x => x.DiaFechamentoFatura == query.DiaFechamentoFatura.Value);
        }

        if (query.DiaVencimentoFatura.HasValue)
        {
            consulta = consulta.Where(x => x.DiaVencimentoFatura == query.DiaVencimentoFatura.Value);
        }

        if (query.ContaBancariaPagamentoPadraoId.HasValue)
        {
            consulta = consulta.Where(x => x.ContaBancariaPagamentoPadraoId == query.ContaBancariaPagamentoPadraoId.Value);
        }

        if (query.Ativo.HasValue)
        {
            consulta = consulta.Where(x => x.Ativo == query.Ativo.Value);
        }

        consulta = (query.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "bandeira" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Bandeira).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.Bandeira).ThenBy(x => x.Nome),
            "numerofinal" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.NumeroFinal).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.NumeroFinal).ThenBy(x => x.Nome),
            "diafechamentofatura" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.DiaFechamentoFatura).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.DiaFechamentoFatura).ThenBy(x => x.Nome),
            "diavencimentofatura" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.DiaVencimentoFatura).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.DiaVencimentoFatura).ThenBy(x => x.Nome),
            "ativo" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Ativo).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.Ativo).ThenBy(x => x.Nome),
            _ => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.Nome)
        };

        var totalItems = await consulta.CountAsync(cancellationToken);
        var selecionados = await consulta.ApplyPagination(query)
            .Select(x => new CartaoProjection(
                x.Id,
                x.Nome,
                x.Bandeira,
                x.NumeroFinal,
                x.DiaFechamentoFatura,
                x.DiaVencimentoFatura,
                x.ContaBancariaPagamentoPadraoId,
                x.LimiteCredito,
                x.Ativo,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        var calculos = await CalcularLimitesAsync(selecionados, cancellationToken);

        var items = selecionados
            .Select(x =>
            {
                var calculo = calculos[x.Id];
                return new CartaoResumoResponse(
                    x.Id,
                    x.Nome,
                    x.Bandeira,
                    x.NumeroFinal,
                    x.DiaFechamentoFatura,
                    x.DiaVencimentoFatura,
                    x.ContaBancariaPagamentoPadraoId,
                    x.LimiteCredito,
                    calculo.UsaLimiteCompartilhado,
                    calculo.LimiteEfetivo,
                    calculo.LimiteComprometido,
                    calculo.LimiteDisponivel,
                    x.Ativo);
            })
            .ToList();

        return PagedResult<CartaoResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);
    }

    public async Task<CartaoDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var cartao = await dbContext.Cartoes.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CartaoProjection(
                x.Id,
                x.Nome,
                x.Bandeira,
                x.NumeroFinal,
                x.DiaFechamentoFatura,
                x.DiaVencimentoFatura,
                x.ContaBancariaPagamentoPadraoId,
                x.LimiteCredito,
                x.Ativo,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

        if (cartao is null)
        {
            return null;
        }

        var calculo = (await CalcularLimitesAsync([cartao], cancellationToken))[cartao.Id];

        return new CartaoDetalheResponse(
            cartao.Id,
            cartao.Nome,
            cartao.Bandeira,
            cartao.NumeroFinal,
            cartao.DiaFechamentoFatura,
            cartao.DiaVencimentoFatura,
            cartao.ContaBancariaPagamentoPadraoId,
            cartao.LimiteCredito,
            calculo.UsaLimiteCompartilhado,
            calculo.LimiteEfetivo,
            calculo.LimiteComprometido,
            calculo.LimiteDisponivel,
            cartao.Ativo,
            cartao.CreatedAtUtc,
            cartao.UpdatedAtUtc);
    }

    public async Task<CartaoDetalheResponse> CriarAsync(CriarCartaoRequest request, CancellationToken cancellationToken)
    {
        await ValidarContaBancariaPadraoAsync(request.ContaBancariaPagamentoPadraoId, cancellationToken);

        Cartao cartao;

        try
        {
            cartao = Cartao.Criar(
                request.Nome,
                request.Bandeira,
                request.NumeroFinal,
                request.DiaFechamentoFatura,
                request.DiaVencimentoFatura,
                request.ContaBancariaPagamentoPadraoId,
                request.LimiteCredito,
                request.Ativo);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            throw ConverterParaValidacao(exception);
        }

        dbContext.Cartoes.Add(cartao);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(cartao.Id, cancellationToken)
            ?? throw new InvalidOperationException("Cartão criado não foi encontrado.");
    }

    public async Task<CartaoDetalheResponse?> AtualizarAsync(
        Guid id,
        AtualizarCartaoRequest request,
        CancellationToken cancellationToken)
    {
        var cartao = await dbContext.Cartoes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (cartao is null)
        {
            return null;
        }

        await ValidarContaBancariaPadraoAsync(request.ContaBancariaPagamentoPadraoId, cancellationToken);

        try
        {
            cartao.Atualizar(
                request.Nome,
                request.Bandeira,
                request.NumeroFinal,
                request.DiaFechamentoFatura,
                request.DiaVencimentoFatura,
                request.ContaBancariaPagamentoPadraoId,
                request.LimiteCredito,
                request.Ativo);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            throw ConverterParaValidacao(exception);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(id, cancellationToken);
    }

    private async Task ValidarContaBancariaPadraoAsync(Guid? contaBancariaId, CancellationToken cancellationToken)
    {
        if (!contaBancariaId.HasValue)
        {
            return;
        }

        var existe = await dbContext.ContasBancarias.AnyAsync(x => x.Id == contaBancariaId.Value, cancellationToken);

        if (!existe)
        {
            throw ValidationExceptionFactory.Create("ContaBancariaPagamentoPadraoId", "Conta bancária não encontrada.");
        }
    }

    private static Exception ConverterParaValidacao(Exception exception)
    {
        var campo = exception switch
        {
            ArgumentException { ParamName: "nome" } => "Nome",
            ArgumentException { ParamName: "bandeira" } => "Bandeira",
            ArgumentException { ParamName: "numeroFinal" } => "NumeroFinal",
            ArgumentOutOfRangeException { ParamName: "diaFechamentoFatura" } => "DiaFechamentoFatura",
            ArgumentOutOfRangeException { ParamName: "diaVencimentoFatura" } => "DiaVencimentoFatura",
            _ => "Request"
        };

        return ValidationExceptionFactory.Create(campo, exception.Message);
    }

    private async Task<Dictionary<Guid, CartaoLimitCalculation>> CalcularLimitesAsync(
        IReadOnlyCollection<CartaoProjection> cartoes,
        CancellationToken cancellationToken)
    {
        if (cartoes.Count == 0)
        {
            return [];
        }

        var cartaoIds = cartoes.Select(x => x.Id).ToArray();
        var contaIds = cartoes
            .Where(x => x.ContaBancariaPagamentoPadraoId.HasValue)
            .Select(x => x.ContaBancariaPagamentoPadraoId!.Value)
            .Distinct()
            .ToArray();

        var comprometimentoPorCartao = await (
            from conta in dbContext.ContasPagar.AsNoTracking()
            where conta.CartaoId.HasValue
                  && conta.StatusContaId != StatusConta.CanceladaId
                  && conta.StatusContaId != StatusConta.LiquidadaId
            where cartaoIds.Contains(conta.CartaoId!.Value)
            group conta by conta.CartaoId!.Value into groupByCartao
            select new
            {
                CartaoId = groupByCartao.Key,
                Valor = groupByCartao.Sum(x => x.ValorLiquido)
            })
            .ToDictionaryAsync(x => x.CartaoId, x => x.Valor, cancellationToken);

var contaInfo = contaIds.Length == 0
            ? new Dictionary<Guid, ContaLimiteInfo>()
            : await (
                from conta in dbContext.ContasBancarias.AsNoTracking()
                where contaIds.Contains(conta.Id)
                select new ContaLimiteInfo(conta.Id, conta.LimiteCartoesCompartilhado))
                .ToDictionaryAsync(x => x.Id, x => x, cancellationToken);

        var comprometimentoPorConta = contaIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : await (
                from contaPagar in dbContext.ContasPagar.AsNoTracking()
                join cartao in dbContext.Cartoes.AsNoTracking() on contaPagar.CartaoId equals cartao.Id
                where cartao.ContaBancariaPagamentoPadraoId.HasValue
                      && contaPagar.StatusContaId != StatusConta.CanceladaId
                      && contaPagar.StatusContaId != StatusConta.LiquidadaId
                group contaPagar by cartao.ContaBancariaPagamentoPadraoId!.Value into groupByConta
                select new
                {
                    ContaBancariaId = groupByConta.Key,
                    Valor = groupByConta.Sum(x => x.ValorLiquido)
                })
                .ToDictionaryAsync(x => x.ContaBancariaId, x => x.Valor, cancellationToken);

        return cartoes.ToDictionary(
            cartao => cartao.Id,
            cartao =>
            {
                var limiteIndividual = cartao.LimiteCredito;
                var limiteComprometidoIndividual = comprometimentoPorCartao.GetValueOrDefault(cartao.Id);

                if (cartao.ContaBancariaPagamentoPadraoId.HasValue &&
                    contaInfo.TryGetValue(cartao.ContaBancariaPagamentoPadraoId.Value, out var info) &&
                    info.LimiteCartoesCompartilhado.HasValue)
                {
                    var limiteEfetivoCompartilhado = info.LimiteCartoesCompartilhado;
                    var limiteComprometidoCompartilhado = comprometimentoPorConta.GetValueOrDefault(cartao.ContaBancariaPagamentoPadraoId.Value);

                    return new CartaoLimitCalculation(
                        true,
                        limiteEfetivoCompartilhado,
                        limiteComprometidoCompartilhado,
                        CalcularDisponivel(limiteEfetivoCompartilhado, limiteComprometidoCompartilhado));
                }

                return new CartaoLimitCalculation(
                    false,
                    limiteIndividual,
                    limiteComprometidoIndividual,
                    CalcularDisponivel(limiteIndividual, limiteComprometidoIndividual));
            });
    }

    private static decimal? CalcularDisponivel(decimal? limite, decimal comprometido)
    {
        return limite.HasValue
            ? decimal.Round(limite.Value - comprometido, 2, MidpointRounding.AwayFromZero)
            : null;
    }

    private sealed record CartaoProjection(
        Guid Id,
        string Nome,
        string Bandeira,
        string NumeroFinal,
        int DiaFechamentoFatura,
        int DiaVencimentoFatura,
        Guid? ContaBancariaPagamentoPadraoId,
        decimal? LimiteCredito,
        bool Ativo,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);

    private sealed record ContaLimiteInfo(Guid Id, decimal? LimiteCartoesCompartilhado);

    private sealed record CartaoLimitCalculation(
        bool UsaLimiteCompartilhado,
        decimal? LimiteEfetivo,
        decimal LimiteComprometido,
        decimal? LimiteDisponivel);
}
