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
            var termo = $"%{query.Search.Trim()}%";
            consulta = consulta.Where(x =>
                EF.Functions.Like(x.Descricao, termo) ||
                (x.Codigo != null && EF.Functions.Like(x.Codigo, termo)));
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

        if (query.AceitaLancamentos.HasValue)
        {
            consulta = query.AceitaLancamentos.Value
                ? consulta.Where(x => !dbContext.ContasGerenciais.Any(child => child.ContaPaiId == x.Id))
                : consulta.Where(x => dbContext.ContasGerenciais.Any(child => child.ContaPaiId == x.Id));
        }

        consulta = (query.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "codigo" => query.SortDirection == SortDirection.Desc
                ? consulta
                    .OrderByDescending(x => x.Codigo == null)
                    .ThenByDescending(x => x.Codigo)
                    .ThenByDescending(x => x.Descricao)
                : consulta
                    .OrderBy(x => x.Codigo == null)
                    .ThenBy(x => x.Codigo)
                    .ThenBy(x => x.Descricao),
            _ => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Descricao)
                : consulta.OrderBy(x => x.Descricao)
        };

        var totalItems = await consulta.CountAsync(cancellationToken);
        var entidades = await consulta.ApplyPagination(query).ToListAsync(cancellationToken);
        var entidadesIds = entidades.Select(x => x.Id).ToArray();
        var contasPai = entidades
            .Where(x => x.ContaPaiId.HasValue)
            .Select(x => x.ContaPaiId!.Value)
            .Distinct()
            .ToArray();
        var contasComFilhos = await dbContext.ContasGerenciais.AsNoTracking()
            .Where(x => x.ContaPaiId.HasValue && entidadesIds.Contains(x.ContaPaiId.Value))
            .Select(x => x.ContaPaiId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var descricoesPai = await dbContext.ContasGerenciais.AsNoTracking()
            .Where(x => contasPai.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Descricao, cancellationToken);
        var responsaveisPadraoIds = entidades
            .Where(x => x.ResponsavelPadraoId.HasValue)
            .Select(x => x.ResponsavelPadraoId!.Value)
            .Distinct()
            .ToArray();
        var responsaveisPadrao = await dbContext.Pessoas.AsNoTracking()
            .Where(x => responsaveisPadraoIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Nome, cancellationToken);
        var contasComFilhosSet = contasComFilhos.ToHashSet();
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
                x.ResponsavelPadraoId,
                x.ResponsavelPadraoId.HasValue && responsaveisPadrao.TryGetValue(x.ResponsavelPadraoId.Value, out var responsavelPadraoNome)
                    ? responsavelPadraoNome
                    : null,
                x.Ativo,
                !contasComFilhosSet.Contains(x.Id),
                x.EhPadraoRecebimentoFaturaCartao))
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
        var responsavelPadraoNome = conta.ResponsavelPadraoId.HasValue
            ? await dbContext.Pessoas.AsNoTracking()
                .Where(pessoa => pessoa.Id == conta.ResponsavelPadraoId.Value)
                .Select(pessoa => pessoa.Nome)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var aceitaLancamentos = !await dbContext.ContasGerenciais.AsNoTracking()
            .AnyAsync(x => x.ContaPaiId == conta.Id, cancellationToken);

        return new ContaGerencialDetalheResponse(
            conta.Id,
            conta.Codigo,
            conta.Descricao,
            MapearTipo(conta.Tipo),
            conta.ContaPaiId,
            contaPaiDescricao,
            conta.ResponsavelPadraoId,
            responsavelPadraoNome,
            conta.Ativo,
            aceitaLancamentos,
            conta.EhPadraoRecebimentoFaturaCartao,
            conta.CreatedAtUtc,
            conta.UpdatedAtUtc);
    }

    public async Task<ContaGerencialDetalheResponse> CriarAsync(
        CriarContaGerencialRequest request,
        CancellationToken cancellationToken)
    {
        await ValidarHierarquiaAsync(null, request.ContaPaiId, cancellationToken);
        var tipoEfetivo = await ResolverTipoEfetivoAsync(request.Tipo, request.ContaPaiId, cancellationToken);
        await ValidarPadraoRecebimentoFaturaAsync(null, tipoEfetivo, request.EhPadraoRecebimentoFaturaCartao, cancellationToken);
        await ValidarResponsavelPadraoAsync(request.ResponsavelPadraoId, cancellationToken);

        ContaGerencial conta;

        try
        {
            conta = ContaGerencial.Criar(
                request.Codigo,
                request.Descricao,
                MapearTipo(tipoEfetivo),
                request.ContaPaiId,
                request.ResponsavelPadraoId,
                request.Ativo,
                request.EhPadraoRecebimentoFaturaCartao);
        }
        catch (ArgumentException exception)
        {
            throw ConverterParaValidacao(exception);
        }

        dbContext.ContasGerenciais.Add(conta);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(conta.Id, cancellationToken)
            ?? throw new InvalidOperationException("Conta gerencial criada não foi encontrada.");
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
        var tipoEfetivo = await ResolverTipoEfetivoAsync(request.Tipo, request.ContaPaiId, cancellationToken);
        await ValidarPadraoRecebimentoFaturaAsync(id, tipoEfetivo, request.EhPadraoRecebimentoFaturaCartao, cancellationToken);
        await ValidarResponsavelPadraoAsync(request.ResponsavelPadraoId, cancellationToken);

        try
        {
            conta.Atualizar(
                request.Codigo,
                request.Descricao,
                MapearTipo(tipoEfetivo),
                request.ContaPaiId,
                request.ResponsavelPadraoId,
                request.Ativo,
                request.EhPadraoRecebimentoFaturaCartao);
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
            throw ValidationExceptionFactory.Create("ContaPaiId", "Conta pai não pode ser a própria conta.");
        }

        var existeContaPai = await dbContext.ContasGerenciais.AnyAsync(x => x.Id == contaPaiId.Value, cancellationToken);

        if (!existeContaPai)
        {
            throw ValidationExceptionFactory.Create("ContaPaiId", "Conta pai não encontrada.");
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

    private async Task<ContaGerencialTipo> ResolverTipoEfetivoAsync(
        ContaGerencialTipo tipoInformado,
        Guid? contaPaiId,
        CancellationToken cancellationToken)
    {
        if (!contaPaiId.HasValue)
        {
            return tipoInformado;
        }

        var tipoContaPai = await dbContext.ContasGerenciais.AsNoTracking()
            .Where(x => x.Id == contaPaiId.Value)
            .Select(x => x.Tipo)
            .SingleAsync(cancellationToken);

        return MapearTipo(tipoContaPai);
    }

    private async Task ValidarPadraoRecebimentoFaturaAsync(
        Guid? contaId,
        ContaGerencialTipo tipo,
        bool ehPadraoRecebimentoFaturaCartao,
        CancellationToken cancellationToken)
    {
        if (!ehPadraoRecebimentoFaturaCartao)
        {
            return;
        }

        if (tipo != ContaGerencialTipo.Receita)
        {
            throw ValidationExceptionFactory.Create(
                "EhPadraoRecebimentoFaturaCartao",
                "Somente contas gerenciais de receita podem ser marcadas como padrão de recebimento de fatura.");
        }

        var existeOutraContaPadrao = await dbContext.ContasGerenciais
            .AnyAsync(
                x => x.EhPadraoRecebimentoFaturaCartao &&
                     (!contaId.HasValue || x.Id != contaId.Value),
                cancellationToken);

        if (existeOutraContaPadrao)
        {
            throw ValidationExceptionFactory.Create(
                "EhPadraoRecebimentoFaturaCartao",
                "Já existe uma conta gerencial padrão para recebimento de fatura.");
        }
    }

    private async Task ValidarResponsavelPadraoAsync(Guid? responsavelPadraoId, CancellationToken cancellationToken)
    {
        if (!responsavelPadraoId.HasValue)
        {
            return;
        }

        var existeResponsavelPadrao = await dbContext.Pessoas
            .AnyAsync(x => x.Id == responsavelPadraoId.Value, cancellationToken);

        if (!existeResponsavelPadrao)
        {
            throw ValidationExceptionFactory.Create("ResponsavelPadraoId", "Responsável padrão não encontrado.");
        }
    }

    private static Exception ConverterParaValidacao(ArgumentException exception)
    {
        var campo = exception.ParamName switch
        {
            "descricao" => "Descricao",
            "contaPaiId" => "ContaPaiId",
            "ehPadraoRecebimentoFaturaCartao" => "EhPadraoRecebimentoFaturaCartao",
            "responsavelPadraoId" => "ResponsavelPadraoId",
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
