using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Cadastros.ContasGerenciais;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Cadastros.ContasGerenciais;

public sealed class ContaGerencialAppService(IAppDbContext dbContext)
{
    public async Task<PagedResult<ContaGerencialResumoResponse>> ListarAsync(
        ContaGerencialListQueryRequest query,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.ContasGerenciais.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = query.Search.Trim().ToLower();
            consulta = consulta.Where(x =>
                x.Descricao.ToLower().Contains(termo) ||
                (x.Codigo != null && x.Codigo.ToLower().Contains(termo)));
        }

        if (query.Tipo.HasValue)
        {
            consulta = consulta.Where(x => x.Tipo == MapearTipo(query.Tipo.Value));
        }

        if (query.ContaPaiId.HasValue)
        {
            consulta = consulta.Where(x => x.ContaPaiId == query.ContaPaiId.Value);
        }

        if (query.Ativo.HasValue)
        {
            consulta = consulta.Where(x => x.Ativo == query.Ativo.Value);
        }

        consulta = query.SortDirection == SortDirection.Desc
            ? consulta.OrderByDescending(x => x.Descricao)
            : consulta.OrderBy(x => x.Descricao);

        var totalItems = await consulta.CountAsync(cancellationToken);
        var entidades = await consulta.ApplyPagination(query).ToListAsync(cancellationToken);
        var contasPai = entidades
            .Where(x => x.ContaPaiId.HasValue)
            .Select(x => x.ContaPaiId!.Value)
            .Distinct()
            .ToArray();
        var descricoesPai = await dbContext.ContasGerenciais.AsNoTracking()
            .Where(x => contasPai.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Descricao, cancellationToken);
        var items = entidades
            .Select(x => new ContaGerencialResumoResponse(
                x.Id,
                x.Codigo,
                x.Descricao,
                MapearTipo(x.Tipo),
                x.ContaPaiId,
                x.ContaPaiId.HasValue && descricoesPai.TryGetValue(x.ContaPaiId.Value, out var descricaoPai)
                    ? descricaoPai
                    : null,
                x.Ativo))
            .ToArray();

        return PagedResult<ContaGerencialResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);
    }

    public async Task<ContaGerencialDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasGerenciais.AsNoTracking()
            .Where(x => x.Id == id)
            .SingleOrDefaultAsync(cancellationToken);

        if (conta is null)
        {
            return null;
        }

        var contaPaiDescricao = conta.ContaPaiId.HasValue
            ? await dbContext.ContasGerenciais.AsNoTracking()
                .Where(parent => parent.Id == conta.ContaPaiId.Value)
                .Select(parent => parent.Descricao)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        return new ContaGerencialDetalheResponse(
            conta.Id,
            conta.Codigo,
            conta.Descricao,
            MapearTipo(conta.Tipo),
            conta.ContaPaiId,
            contaPaiDescricao,
            conta.Ativo,
            conta.CreatedAtUtc,
            conta.UpdatedAtUtc);
    }

    public async Task<ContaGerencialDetalheResponse> CriarAsync(
        CriarContaGerencialRequest request,
        CancellationToken cancellationToken)
    {
        await ValidarHierarquiaAsync(null, request.ContaPaiId, cancellationToken);

        ContaGerencial conta;

        try
        {
            conta = ContaGerencial.Criar(
                request.Codigo,
                request.Descricao,
                MapearTipo(request.Tipo),
                request.ContaPaiId,
                request.Ativo);
        }
        catch (ArgumentException exception)
        {
            throw ConverterParaValidacao(exception);
        }

        dbContext.ContasGerenciais.Add(conta);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(conta.Id, cancellationToken)
            ?? throw new InvalidOperationException("Conta gerencial criada nao foi encontrada.");
    }

    public async Task<ContaGerencialDetalheResponse?> AtualizarAsync(
        Guid id,
        AtualizarContaGerencialRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasGerenciais.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        await ValidarHierarquiaAsync(id, request.ContaPaiId, cancellationToken);

        try
        {
            conta.Atualizar(
                request.Codigo,
                request.Descricao,
                MapearTipo(request.Tipo),
                request.ContaPaiId,
                request.Ativo);
        }
        catch (ArgumentException exception)
        {
            throw ConverterParaValidacao(exception);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(id, cancellationToken);
    }

    private async Task ValidarHierarquiaAsync(Guid? contaId, Guid? contaPaiId, CancellationToken cancellationToken)
    {
        if (!contaPaiId.HasValue)
        {
            return;
        }

        if (contaId.HasValue && contaId.Value == contaPaiId.Value)
        {
            throw ValidationExceptionFactory.Create("ContaPaiId", "Conta pai nao pode ser a propria conta.");
        }

        var existeContaPai = await dbContext.ContasGerenciais.AnyAsync(x => x.Id == contaPaiId.Value, cancellationToken);

        if (!existeContaPai)
        {
            throw ValidationExceptionFactory.Create("ContaPaiId", "Conta pai nao encontrada.");
        }

        var proximaContaPai = await dbContext.ContasGerenciais
            .Where(x => x.Id == contaPaiId.Value)
            .Select(x => x.ContaPaiId)
            .SingleOrDefaultAsync(cancellationToken);

        while (proximaContaPai.HasValue)
        {
            if (contaId.HasValue && proximaContaPai.Value == contaId.Value)
            {
                throw ValidationExceptionFactory.Create("ContaPaiId", "A hierarquia informada gera ciclo.");
            }

            proximaContaPai = await dbContext.ContasGerenciais
                .Where(x => x.Id == proximaContaPai.Value)
                .Select(x => x.ContaPaiId)
                .SingleOrDefaultAsync(cancellationToken);
        }
    }

    private static Exception ConverterParaValidacao(ArgumentException exception)
    {
        var campo = exception.ParamName switch
        {
            "descricao" => "Descricao",
            "contaPaiId" => "ContaPaiId",
            _ => "Request"
        };

        return ValidationExceptionFactory.Create(campo, exception.Message);
    }

    private static TipoContaGerencial MapearTipo(ContaGerencialTipo tipo)
    {
        return tipo switch
        {
            ContaGerencialTipo.Receita => TipoContaGerencial.Receita,
            ContaGerencialTipo.Despesa => TipoContaGerencial.Despesa,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }

    private static ContaGerencialTipo MapearTipo(TipoContaGerencial tipo)
    {
        return tipo switch
        {
            TipoContaGerencial.Receita => ContaGerencialTipo.Receita,
            TipoContaGerencial.Despesa => ContaGerencialTipo.Despesa,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }
}
