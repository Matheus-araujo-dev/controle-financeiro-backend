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
    public async Task<PagedResult<PessoaResumoResponse>> ListarAsync(PessoaListQueryRequest query, CancellationToken cancellationToken)
    {
        var consulta = dbContext.Pessoas.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = $"%{query.Search.Trim()}%";
            consulta = consulta.Where(x =>
                EF.Functions.Like(x.Nome, termo) ||
                (x.CpfCnpj != null && EF.Functions.Like(x.CpfCnpj, termo)) ||
                (x.Email != null && EF.Functions.Like(x.Email, termo)));
        }

        if (query.TipoPessoa.HasValue)
        {
            consulta = consulta.Where(x => x.TipoPessoa == MapearTipoPessoa(query.TipoPessoa.Value));
        }

        if (query.Ativo.HasValue)
        {
            consulta = consulta.Where(x => x.Ativo == query.Ativo.Value);
        }

        consulta = (query.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "tipopessoa" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.TipoPessoa).ThenByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.TipoPessoa).ThenBy(x => x.Nome),
            _ => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Nome)
                : consulta.OrderBy(x => x.Nome)
        };

        var totalItems = await consulta.CountAsync(cancellationToken);
        var entidades = await consulta
            .ApplyPagination(query)
            .ToListAsync(cancellationToken);

        var items = entidades
            .Select(x => new PessoaResumoResponse(
                x.Id,
                x.Nome,
                MapearTipoPessoa(x.TipoPessoa),
                MascaraDocumento(x.CpfCnpj),
                x.Email,
                x.Telefone,
                x.Ativo))
            .ToArray();

        return PagedResult<PessoaResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);
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

    private static string? MascaraDocumento(string? documento)
    {
        if (string.IsNullOrEmpty(documento))
        {
            return string.Empty;
        }

        if (documento.Length <= 4)
        {
            return documento;
        }

        return new string('*', documento.Length - 4) + documento[^4..];
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
            MascaraDocumento(pessoa.CpfCnpj),
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
