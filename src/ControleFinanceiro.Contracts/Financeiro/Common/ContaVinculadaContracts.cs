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
    DateOnly DataVencimento,
    string? PessoaNome = null,
    int NumeroParcela = 1,
    int QuantidadeParcelas = 1);

/// <summary>Campo adicional de request que suporta lista de responsáveis.
/// Quando informado com 2+ itens, cria uma conta por responsável com valor dividido igualmente.</summary>
public sealed record ResponsaveisAdicionaisRequest(IReadOnlyList<Guid> ResponsaveisIds);
