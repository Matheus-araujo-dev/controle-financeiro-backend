using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Cadastros.Pessoas;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Cadastros.Pessoas;

public sealed class PessoaAppService(IAppDbContext dbContext)
{
    public async Task<PessoaListResponse> ListarAsync(PessoaListQueryRequest query, CancellationToken cancellationToken)
    {
        var consulta = dbContext.Pessoas.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = $"%{query.Search.Trim().ToLower()}%";
            consulta = consulta.Where(x =>
                EF.Functions.Like(x.Nome.ToLower(), termo) ||
                (x.CpfCnpj != null && EF.Functions.Like(x.CpfCnpj.ToLower(), termo)) ||
                (x.Email != null && EF.Functions.Like(x.Email.ToLower(), termo)));
        }

        var tipos = MapearTiposFiltro(query);
        if (tipos.Count > 0)
        {
            consulta = consulta.Where(x => tipos.Contains(x.TipoPessoa));
        }

        if (query.Ativo.HasValue)
        {
            consulta = consulta.Where(x => x.Ativo == query.Ativo.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Documento))
        {
            var termoDoc = $"%{query.Documento.Trim().ToLower()}%";
            consulta = consulta.Where(x => x.CpfCnpj != null && EF.Functions.Like(x.CpfCnpj.ToLower(), termoDoc));
        }

        if (!string.IsNullOrWhiteSpace(query.Email))
        {
            var termoEmail = $"%{query.Email.Trim().ToLower()}%";
            consulta = consulta.Where(x => x.Email != null && EF.Functions.Like(x.Email.ToLower(), termoEmail));
        }

        if (!string.IsNullOrWhiteSpace(query.Telefone))
        {
            var termoTelefone = $"%{query.Telefone.Trim().ToLower()}%";
            consulta = consulta.Where(x => x.Telefone != null && EF.Functions.Like(x.Telefone.ToLower(), termoTelefone));
        }

        // Totalizadores sobre o conjunto FILTRADO (antes da paginação).
        var summaryRaw = await consulta
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Ativos = g.Count(x => x.Ativo),
                Fisicas = g.Count(x => x.TipoPessoa == TipoPessoa.Fisica),
                Juridicas = g.Count(x => x.TipoPessoa == TipoPessoa.Juridica)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var summary = new PessoaListSummaryResponse(
            summaryRaw?.Total ?? 0,
            summaryRaw?.Ativos ?? 0,
            (summaryRaw?.Total ?? 0) - (summaryRaw?.Ativos ?? 0),
            summaryRaw?.Fisicas ?? 0,
            summaryRaw?.Juridicas ?? 0);

        var totalItems = summary.Total;

        consulta = (query.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "tipopessoa" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.TipoPessoa).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.TipoPessoa).ThenBy(x => x.Nome),
            "cpfcnpj" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.CpfCnpj).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.CpfCnpj).ThenBy(x => x.Nome),
            "email" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Email).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.Email).ThenBy(x => x.Nome),
            "telefone" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Telefone).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.Telefone).ThenBy(x => x.Nome),
            "ativo" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Ativo).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.Ativo).ThenBy(x => x.Nome),
            _ => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.Nome)
        };

        var entidades = await consulta
            .ApplyPagination(query)
            .ToListAsync(cancellationToken);

        var items = entidades
            .Select(x => new PessoaResumoResponse(
                x.Id,
                x.Nome,
                MapearTipoPessoa(x.TipoPessoa),
                x.CpfCnpj,
                x.Email,
                x.Telefone,
                x.Ativo))
            .ToArray();

        var paged = PagedResult<PessoaResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);

        return new PessoaListResponse(
            paged.Items,
            paged.Page,
            paged.PageSize,
            paged.TotalItems,
            paged.TotalPages,
            summary);
    }

    private static IReadOnlyCollection<TipoPessoa> MapearTiposFiltro(PessoaListQueryRequest query)
    {
        var tipos = new List<TipoPessoa>();

        if (query.TiposPessoa is { Count: > 0 })
        {
            tipos.AddRange(query.TiposPessoa.Select(MapearTipoPessoa));
        }

        if (query.TipoPessoa.HasValue)
        {
            tipos.Add(MapearTipoPessoa(query.TipoPessoa.Value));
        }

        return tipos.Distinct().ToArray();
    }

    public async Task<PessoaDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var pessoa = await dbContext.Pessoas
            .AsNoTracking()
            .Include(x => x.ChavesPix)
            .Where(x => x.Id == id)
            .SingleOrDefaultAsync(cancellationToken);

        return pessoa is null ? null : MapearDetalhe(pessoa);
    }

    public async Task<PessoaDetalheResponse> CriarAsync(CriarPessoaRequest request, CancellationToken cancellationToken)
    {
        var documentoNormalizado = NormalizarDocumento(request.CpfCnpj);

        if (documentoNormalizado is not null &&
            await dbContext.Pessoas.AnyAsync(x => x.CpfCnpj == documentoNormalizado, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("CpfCnpj", "CpfCnpj ja cadastrado.");
        }

        var pessoa = CriarPessoa(request);

        dbContext.Pessoas.Add(pessoa);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapearDetalhe(pessoa);
    }

    public async Task<PessoaDetalheResponse?> AtualizarAsync(
        Guid id,
        AtualizarPessoaRequest request,
        CancellationToken cancellationToken)
    {
        var pessoa = await dbContext.Pessoas
            .Include(x => x.ChavesPix)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (pessoa is null)
        {
            return null;
        }

        var documentoNormalizado = NormalizarDocumento(request.CpfCnpj);

        if (documentoNormalizado is not null &&
            await dbContext.Pessoas.AnyAsync(x => x.CpfCnpj == documentoNormalizado && x.Id != id, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("CpfCnpj", "CpfCnpj ja cadastrado.");
        }

        try
        {
            var chavesPix = MapearChavesPix(request.ChavesPix);

            pessoa.AtualizarDadosBasicos(
                request.Nome,
                MapearTipoPessoa(request.TipoPessoa),
                request.CpfCnpj,
                request.Email,
                request.Telefone,
                request.Observacao,
                pessoa.Ativo);

            if (pessoa.ChavesPix.Count > 0)
            {
                dbContext.PessoasChavesPix.RemoveRange(pessoa.ChavesPix);
                pessoa.SubstituirChavesPix([]);
            }

            pessoa.SubstituirChavesPix(chavesPix);

            if (pessoa.ChavesPix.Count > 0)
            {
                dbContext.PessoasChavesPix.AddRange(pessoa.ChavesPix);
            }
        }
        catch (ArgumentException exception)
        {
            throw ConverterParaValidacao(exception);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapearDetalhe(pessoa);
    }

    public async Task<PessoaDetalheResponse?> DefinirAtivacaoAsync(Guid id, bool ativo, CancellationToken cancellationToken)
    {
        var pessoa = await dbContext.Pessoas.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (pessoa is null)
        {
            return null;
        }

        if (ativo)
        {
            pessoa.Ativar();
        }
        else
        {
            pessoa.Inativar();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapearDetalhe(pessoa);
    }

    private static Pessoa CriarPessoa(CriarPessoaRequest request)
    {
        try
        {
            return Pessoa.Criar(
                request.Nome,
                MapearTipoPessoa(request.TipoPessoa),
                request.CpfCnpj,
                request.Email,
                request.Telefone,
                request.Observacao,
                MapearChavesPix(request.ChavesPix),
                true);
        }
        catch (ArgumentException exception)
        {
            throw ConverterParaValidacao(exception);
        }
    }

    private static ApplicationValidationException ConverterParaValidacao(ArgumentException exception)
    {
        var campo = exception.ParamName switch
        {
            "nome" => "Nome",
            "chavesPix" => "ChavesPix",
            "chave" => "ChavesPix",
            _ => "Request"
        };

        return ValidationExceptionFactory.Create(campo, exception.Message);
    }

    private static string? NormalizarDocumento(string? cpfCnpj)
    {
        if (string.IsNullOrWhiteSpace(cpfCnpj))
        {
            return null;
        }

        var digitos = new string(cpfCnpj.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digitos) ? null : digitos;
    }

    private static TipoPessoa MapearTipoPessoa(PessoaTipo tipoPessoa)
    {
        return tipoPessoa switch
        {
            PessoaTipo.Fisica => TipoPessoa.Fisica,
            PessoaTipo.Juridica => TipoPessoa.Juridica,
            _ => throw new ArgumentOutOfRangeException(nameof(tipoPessoa))
        };
    }

    private static PessoaTipo MapearTipoPessoa(TipoPessoa tipoPessoa)
    {
        return tipoPessoa switch
        {
            TipoPessoa.Fisica => PessoaTipo.Fisica,
            TipoPessoa.Juridica => PessoaTipo.Juridica,
            _ => throw new ArgumentOutOfRangeException(nameof(tipoPessoa))
        };
    }

    private static PessoaDetalheResponse MapearDetalhe(Pessoa pessoa)
    {
        return new PessoaDetalheResponse(
            pessoa.Id,
            pessoa.Nome,
            MapearTipoPessoa(pessoa.TipoPessoa),
            pessoa.CpfCnpj,
            pessoa.Email,
            pessoa.Telefone,
            pessoa.Observacao,
            pessoa.ChavesPix
                .OrderBy(x => x.Tipo)
                .ThenBy(x => x.Chave)
                .Select(x => new PessoaChavePixResponse(MapearTipoChavePix(x.Tipo), x.Chave))
                .ToArray(),
            pessoa.Ativo,
            pessoa.CreatedAtUtc,
            pessoa.UpdatedAtUtc);
    }

    private static IReadOnlyCollection<ChavePixPlano> MapearChavesPix(IReadOnlyCollection<PessoaChavePixRequest>? chavesPix)
    {
        return (chavesPix ?? [])
            .Select(item => ChavePixPlano.Create(MapearTipoChavePix(item.Tipo), item.Chave))
            .ToArray();
    }

    private static TipoChavePix MapearTipoChavePix(PessoaChavePixTipo tipo)
    {
        return tipo switch
        {
            PessoaChavePixTipo.CpfCnpj => TipoChavePix.CpfCnpj,
            PessoaChavePixTipo.Email => TipoChavePix.Email,
            PessoaChavePixTipo.Telefone => TipoChavePix.Telefone,
            PessoaChavePixTipo.Aleatoria => TipoChavePix.Aleatoria,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }

    private static PessoaChavePixTipo MapearTipoChavePix(TipoChavePix tipo)
    {
        return tipo switch
        {
            TipoChavePix.CpfCnpj => PessoaChavePixTipo.CpfCnpj,
            TipoChavePix.Email => PessoaChavePixTipo.Email,
            TipoChavePix.Telefone => PessoaChavePixTipo.Telefone,
            TipoChavePix.Aleatoria => PessoaChavePixTipo.Aleatoria,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }
}
