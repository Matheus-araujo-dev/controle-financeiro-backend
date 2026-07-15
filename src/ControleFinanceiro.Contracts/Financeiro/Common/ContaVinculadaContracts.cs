namespace ControleFinanceiro.Contracts.Financeiro.Common;

public enum TipoContaVinculada
{
    Pagar = 1,
    Receber = 2
}

public sealed record ContaVinculadaResumo(
    Guid Id,
    TipoContaVinculada Tipo,
    string Descricao,
    decimal ValorLiquido,
    string StatusCodigo,
    string StatusNome,
    DateOnly DataVencimento);
