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
            var termo = query.Search.Trim().ToLower();
            consulta = consulta.Where(x =>
                x.Nome.ToLower().Contains(termo) ||
                (x.CpfCnpj != null && x.CpfCnpj.ToLower().Contains(termo)) ||
                (x.Email != null && x.Email.ToLower().Contains(termo)));
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
                x.CpfCnpj,
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
        var pessoa = await dbContext.Pessoas.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

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
            pessoa.Atualizar(
                request.Nome,
                MapearTipoPessoa(request.TipoPessoa),
                request.CpfCnpj,
                request.Email,
                request.Telefone,
                request.Observacao,
                pessoa.Ativo);
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
            pessoa.Ativo,
            pessoa.CreatedAtUtc,
            pessoa.UpdatedAtUtc);
    }
}
