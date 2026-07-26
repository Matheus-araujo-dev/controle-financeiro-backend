using ControleFinanceiro.Application.Common.Cache;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.Reembolsos;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using RateioRequest = ControleFinanceiro.Contracts.Financeiro.Common.RateioRequest;

namespace ControleFinanceiro.Application.Financeiro.Reembolsos;

public interface IReembolsoAppService
{
    Task<CriarReembolsoContaPagarResponse> CriarReembolsoContaPagarAsync(
        CriarReembolsoContaPagarRequest request, CancellationToken cancellationToken);
}

public sealed class ReembolsoAppService(
    IAppDbContext dbContext,
    ILookupCacheService lookupCache) : IReembolsoAppService
{
    public async Task<CriarReembolsoContaPagarResponse> CriarReembolsoContaPagarAsync(
        CriarReembolsoContaPagarRequest request,
        CancellationToken cancellationToken)
    {
        // ── Validar request ──────────────────────────────────────────────────
        if (request.PagadoresIds is null || request.PagadoresIds.Count == 0)
            throw ValidationExceptionFactory.Create("PagadoresIds", "Informe ao menos um responsável pelo reembolso.");

        if (request.ValorTotal <= 0)
            throw ValidationExceptionFactory.Create("ValorTotal", "Valor do reembolso deve ser maior que zero.");

        var formaPagamento = await lookupCache.GetFormaPagamentoByIdAsync(request.FormaPagamentoId, cancellationToken)
            ?? throw ValidationExceptionFactory.Create("FormaPagamentoId", "Forma de pagamento não encontrada.");

        // Validar pagadores
        var pagadorIds = request.PagadoresIds.Distinct().ToArray();
        var pagadoresExistentes = await dbContext.Pessoas
            .Where(x => pagadorIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Nome })
            .ToArrayAsync(cancellationToken);

        if (pagadoresExistentes.Length != pagadorIds.Length)
            throw ValidationExceptionFactory.Create("PagadoresIds", "Um ou mais responsáveis não foram encontrados.");

        var nomePorPagador = pagadoresExistentes.ToDictionary(x => x.Id, x => x.Nome);

        // ── Carregar conta de origem e grupo de parcelas ─────────────────────
        var contaOrigem = await dbContext.ContasPagar
            .SingleOrDefaultAsync(x => x.Id == request.ContaOrigemId, cancellationToken)
            ?? throw ValidationExceptionFactory.Create("ContaOrigemId", "Conta a pagar de origem não encontrada.");

        // Verificar se já tem reembolso gerado
        if (contaOrigem.GrupoReembolsoId.HasValue)
            throw ValidationExceptionFactory.Create("ContaOrigemId", "Esta conta já possui um reembolso gerado. Cancele o reembolso existente antes de criar um novo.");

        IReadOnlyList<ContaPagar> parcelasOrigem;
        if (request.ParcelarIgual && contaOrigem.GrupoParcelamentoId.HasValue)
        {
            parcelasOrigem = await dbContext.ContasPagar
                .Where(x => x.GrupoParcelamentoId == contaOrigem.GrupoParcelamentoId)
                .OrderBy(x => x.NumeroParcela)
                .ToListAsync(cancellationToken);
        }
        else
        {
            parcelasOrigem = [contaOrigem];
        }

        var quantidadeParcelas = request.ParcelarIgual ? parcelasOrigem.Count : 1;

        // Validar rateios
        var rateioIds = request.Rateios.Select(r => r.ContaGerencialId).Distinct().ToArray();
        await ContaGerencialLancamentoValidator.ValidarContasLancaveisPorTipoAsync(
            dbContext,
            rateioIds,
            ControleFinanceiro.Domain.Cadastros.ContasGerenciais.TipoContaGerencial.Receita,
            "Rateios",
            "Uma ou mais contas gerenciais não foram encontradas.",
            "Somente contas gerenciais filhas podem ser usadas em rateios.",
            "Contas a receber aceitam apenas contas gerenciais de receita.",
            cancellationToken);

        // ── Distribuir valor por pagador ──────────────────────────────────────
        var valorTotalPorPagador = DistribuirValorPorPagador(request.ValorTotal, pagadorIds.Length);
        var grupoReembolsoId = Guid.NewGuid();
        var todasContasReceber = new List<ContaReceber>();

        for (var iPagador = 0; iPagador < pagadorIds.Length; iPagador++)
        {
            var pagadorId = pagadorIds[iPagador];
            var valorPagador = valorTotalPorPagador[iPagador];
            var grupoResponsaveisId = Guid.NewGuid(); // um grupo por pagador

            // Distribuir valor do pagador pelas parcelas
            var valoresParcelas = DistribuirValorPorPagador(valorPagador, quantidadeParcelas);
            var valorLiquidoTotal = valoresParcelas.Sum();
            var rateios = ConverterRateios(request.Rateios);

            for (var iParcela = 0; iParcela < quantidadeParcelas; iParcela++)
            {
                var pagarParcela = parcelasOrigem[iParcela];
                var valorParcela = valoresParcelas[iParcela];
                var dataVenc = request.ParcelarIgual ? pagarParcela.DataVencimento : request.DataVencimento;
                var rateiosParcela = ParcelamentoHelper.DistribuirRateios(rateios, valorParcela, valorLiquidoTotal);

                var descricaoParcela = quantidadeParcelas > 1
                    ? AjustarDescricaoParcela(request.Descricao, iParcela + 1, quantidadeParcelas)
                    : request.Descricao;

                var conta = ContaReceber.Criar(
                    null,
                    DateOnly.FromDateTime(DateTime.Today),
                    null,
                    pagadorId,
                    dataVenc,
                    request.FormaPagamentoId,
                    null,
                    null,
                    valorParcela,
                    0m, 0m, 0m,
                    quantidadeParcelas,
                    iParcela + 1,
                    quantidadeParcelas > 1 ? grupoResponsaveisId : (Guid?)null,
                    descricaoParcela,
                    request.Observacao,
                    StatusConta.PendenteId,
                    false,
                    null,
                    OrigemLancamento.Manual,
                    rateiosParcela);

                conta.DefinirGrupoReembolso(grupoReembolsoId);
                conta.DefinirGrupoResponsaveis(grupoResponsaveisId);
                conta.VincularContaContraria(pagarParcela.Id, Domain.Financeiro.TipoContaVinculada.Pagar);

                todasContasReceber.Add(conta);
            }
        }

        // ── Persistir ────────────────────────────────────────────────────────
        dbContext.ContasReceber.AddRange(todasContasReceber);
        dbContext.RateiosContaGerencial.AddRange(todasContasReceber.SelectMany(x => x.Rateios));

        // Marcar todas as contas_pagar do grupo com GrupoReembolsoId
        foreach (var pagar in parcelasOrigem)
            pagar.DefinirGrupoReembolso(grupoReembolsoId);

        await dbContext.SaveChangesAsync(cancellationToken);

        // ── Construir response ────────────────────────────────────────────────
        var resumos = todasContasReceber.Select(cr => new ReembolsoContaResumo(
            cr.Id,
            cr.PagadorId,
            nomePorPagador[cr.PagadorId],
            cr.NumeroParcela,
            cr.QuantidadeParcelas,
            cr.ValorLiquido,
            cr.DataVencimento,
            cr.Descricao)).ToList();

        return new CriarReembolsoContaPagarResponse(grupoReembolsoId, resumos);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static decimal[] DistribuirValorPorPagador(decimal valorTotal, int quantidade)
    {
        var valores = ParcelamentoHelper.Distribuir(valorTotal, quantidade);
        return valores.ToArray();
    }

    private static IReadOnlyCollection<RateioPlano> ConverterRateios(IReadOnlyList<RateioRequest> rateios) =>
        rateios.Select(r => RateioPlano.Create(r.ContaGerencialId, r.Valor)).ToArray();

    private static readonly System.Text.RegularExpressions.Regex _parcelaRegex = new(
        @"(?<!\d)(?<atual>\d{1,2})\s*/\s*(?<total>\d{1,2})(?!\d)",
        System.Text.RegularExpressions.RegexOptions.Compiled |
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string AjustarDescricaoParcela(string descricao, int numeroParcela, int quantidadeParcelas)
    {
        if (string.IsNullOrWhiteSpace(descricao)) return descricao;
        var semMarcador = _parcelaRegex.Replace(descricao, string.Empty).Trim();
        return $"{semMarcador} {numeroParcela}/{quantidadeParcelas}";
    }
}
