using ControleFinanceiro.Contracts.Financeiro.Common;

namespace ControleFinanceiro.Contracts.Financeiro.Reembolsos;

/// <summary>
/// Cria contas a receber (reembolso) a partir de uma conta a pagar existente.
/// Quando ParcelarIgual = true, gera uma conta por parcela original × pagador.
/// Quando ParcelarIgual = false, gera uma conta por pagador com DataVencimento informado.
/// </summary>
public sealed record CriarReembolsoContaPagarRequest(
    /// <summary>Id de qualquer parcela do grupo de contas a pagar de origem.</summary>
    Guid ContaOrigemId,
    /// <summary>Se verdadeiro, distribui o reembolso nas mesmas datas das parcelas originais.</summary>
    bool ParcelarIgual,
    /// <summary>Valor total do reembolso (pode diferir do valor original).</summary>
    decimal ValorTotal,
    /// <summary>Um ou mais responsáveis que pagarão o reembolso. Cada um gera contas independentes.</summary>
    IReadOnlyList<Guid> PagadoresIds,
    Guid FormaPagamentoId,
    /// <summary>Usado apenas quando ParcelarIgual = false.</summary>
    DateOnly DataVencimento,
    string Descricao,
    string? Observacao,
    IReadOnlyList<RateioRequest> Rateios);

/// <summary>Resumo de uma conta gerada no reembolso (para exibição em lote).</summary>
public sealed record ReembolsoContaResumo(
    Guid Id,
    Guid PagadorId,
    string PagadorNome,
    int NumeroParcela,
    int QuantidadeParcelas,
    decimal ValorLiquido,
    DateOnly DataVencimento,
    string Descricao);

public sealed record CriarReembolsoContaPagarResponse(
    Guid GrupoReembolsoId,
    IReadOnlyList<ReembolsoContaResumo> ContasReceber);

/// <summary>Resumo de grupo de reembolso retornado no detalhe de ContaPagar/ContaReceber.</summary>
public sealed record GrupoReembolsoInfo(
    Guid GrupoReembolsoId,
    IReadOnlyList<ContaVinculadaResumo> Contas);

/// <summary>Resumo do grupo de responsáveis de um mesmo lançamento.</summary>
public sealed record GrupoResponsaveisInfo(
    Guid GrupoResponsaveisId,
    IReadOnlyList<ContaVinculadaResumo> Contas);
