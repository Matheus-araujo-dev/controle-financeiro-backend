using ControleFinanceiro.Application.Common.Extensions;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Common.Validation;

internal static class ContaGerencialLancamentoValidator
{
    public static Task ValidarContasLancaveisAsync(
        IAppDbContext dbContext,
        IReadOnlyCollection<Guid> contaGerencialIds,
        string campo,
        string mensagemNaoEncontrada,
        string mensagemContaPai,
        CancellationToken cancellationToken)
    {
        return ValidarInternoAsync(
            dbContext,
            contaGerencialIds.Distinct().ToArray(),
            campo,
            mensagemNaoEncontrada,
            mensagemContaPai,
            null,
            null,
            cancellationToken);
    }

    public static Task ValidarContaLancavelAsync(
        IAppDbContext dbContext,
        Guid contaGerencialId,
        string campo,
        string mensagemNaoEncontrada,
        string mensagemContaPai,
        CancellationToken cancellationToken)
    {
        return ValidarInternoAsync(
            dbContext,
            [contaGerencialId],
            campo,
            mensagemNaoEncontrada,
            mensagemContaPai,
            null,
            null,
            cancellationToken);
    }

    public static Task ValidarContasLancaveisPorTipoAsync(
        IAppDbContext dbContext,
        IReadOnlyCollection<Guid> contaGerencialIds,
        TipoContaGerencial tipoEsperado,
        string campo,
        string mensagemNaoEncontrada,
        string mensagemContaPai,
        string mensagemTipoInvalido,
        CancellationToken cancellationToken)
    {
        return ValidarInternoAsync(
            dbContext,
            contaGerencialIds.Distinct().ToArray(),
            campo,
            mensagemNaoEncontrada,
            mensagemContaPai,
            tipoEsperado,
            mensagemTipoInvalido,
            cancellationToken);
    }

    public static Task ValidarContaLancavelPorTipoAsync(
        IAppDbContext dbContext,
        Guid contaGerencialId,
        TipoContaGerencial tipoEsperado,
        string campo,
        string mensagemNaoEncontrada,
        string mensagemContaPai,
        string mensagemTipoInvalido,
        CancellationToken cancellationToken)
    {
        return ValidarInternoAsync(
            dbContext,
            [contaGerencialId],
            campo,
            mensagemNaoEncontrada,
            mensagemContaPai,
            tipoEsperado,
            mensagemTipoInvalido,
            cancellationToken);
    }

    private static async Task ValidarInternoAsync(
        IAppDbContext dbContext,
        Guid[] contaGerencialIds,
        string campo,
        string mensagemNaoEncontrada,
        string mensagemContaPai,
        TipoContaGerencial? tipoEsperado,
        string? mensagemTipoInvalido,
        CancellationToken cancellationToken)
    {
        if (contaGerencialIds.Length == 0)
        {
            return;
        }

        var contasGerenciaisEncontradas = await dbContext.ContasGerenciais
            .WhereIn(x => x.Id, contaGerencialIds)
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);

        if (contasGerenciaisEncontradas.Length != contaGerencialIds.Length)
        {
            throw ValidationExceptionFactory.Create(campo, mensagemNaoEncontrada);
        }

        var contasPaiSelecionadas = await dbContext.ContasGerenciais
            .Where(x => x.ContaPaiId.HasValue)
            .WhereIn(x => x.ContaPaiId!.Value, contaGerencialIds)
            .Select(x => x.ContaPaiId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        if (contasPaiSelecionadas.Length > 0)
        {
            throw ValidationExceptionFactory.Create(campo, mensagemContaPai);
        }

        if (tipoEsperado.HasValue)
        {
            var possuiTipoInvalido = await dbContext.ContasGerenciais
                .WhereIn(x => x.Id, contaGerencialIds)
                .AnyAsync(x => x.Tipo != tipoEsperado.Value, cancellationToken);

            if (possuiTipoInvalido)
            {
                throw ValidationExceptionFactory.Create(campo, mensagemTipoInvalido ?? mensagemNaoEncontrada);
            }
        }
    }
}
