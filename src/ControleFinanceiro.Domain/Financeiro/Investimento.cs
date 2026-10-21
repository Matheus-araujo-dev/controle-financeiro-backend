using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Financeiro;

public enum TipoInvestimento
{
    RendaFixa = 1,
    RendaVariavel = 2,
    FundoImobiliario = 3,
    Criptomoeda = 4,
    Outro = 5
}

public enum LiquidezInvestimento
{
    Diaria = 1,
    Vencimento = 2,
    Iliquido = 3
}

public sealed class Investimento : TenantEntity
{
    private Investimento()
    {
    }

    public string Nome { get; private set; } = string.Empty;

    public string? Emissor { get; private set; }

    public TipoInvestimento Tipo { get; private set; }

    public LiquidezInvestimento Liquidez { get; private set; }

    public decimal ValorInvestido { get; private set; }

    public decimal ValorAtual { get; private set; }

    public DateOnly DataAplicacao { get; private set; }

    public DateOnly? DataVencimento { get; private set; }

    public decimal? TaxaAnual { get; private set; }

    public Guid ContaBancariaVinculadaId { get; private set; }

    public bool Encerrado { get; private set; }

    public decimal Rendimento => ValorAtual - ValorInvestido;

    public decimal RendimentoPercent => ValorInvestido > 0 ? (Rendimento / ValorInvestido) * 100m : 0m;

    public static Investimento Criar(
        string nome,
        string? emissor,
        TipoInvestimento tipo,
        LiquidezInvestimento liquidez,
        decimal valorInvestido,
        DateOnly dataAplicacao,
        DateOnly? dataVencimento,
        decimal? taxaAnual,
        Guid contaBancariaVinculadaId)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome é obrigatório.", nameof(nome));

        if (valorInvestido <= 0)
            throw new ArgumentException("Valor investido deve ser positivo.", nameof(valorInvestido));

        if (contaBancariaVinculadaId == Guid.Empty)
            throw new ArgumentException("Conta bancária vinculada é obrigatória.", nameof(contaBancariaVinculadaId));

        if (taxaAnual.HasValue && taxaAnual.Value < 0)
            throw new ArgumentException("Taxa anual não pode ser negativa.", nameof(taxaAnual));

        return new Investimento
        {
            Nome = nome.Trim(),
            Emissor = string.IsNullOrWhiteSpace(emissor) ? null : emissor.Trim(),
            Tipo = tipo,
            Liquidez = liquidez,
            ValorInvestido = decimal.Round(valorInvestido, 2),
            ValorAtual = decimal.Round(valorInvestido, 2),
            DataAplicacao = dataAplicacao,
            DataVencimento = dataVencimento,
            TaxaAnual = taxaAnual.HasValue ? decimal.Round(taxaAnual.Value, 4) : null,
            ContaBancariaVinculadaId = contaBancariaVinculadaId,
            Encerrado = false
        };
    }

    public void Atualizar(
        string nome,
        string? emissor,
        TipoInvestimento tipo,
        LiquidezInvestimento liquidez,
        DateOnly? dataVencimento,
        decimal? taxaAnual)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome é obrigatório.", nameof(nome));

        if (taxaAnual.HasValue && taxaAnual.Value < 0)
            throw new ArgumentException("Taxa anual não pode ser negativa.", nameof(taxaAnual));

        Nome = nome.Trim();
        Emissor = string.IsNullOrWhiteSpace(emissor) ? null : emissor.Trim();
        Tipo = tipo;
        Liquidez = liquidez;
        DataVencimento = dataVencimento;
        TaxaAnual = taxaAnual.HasValue ? decimal.Round(taxaAnual.Value, 4) : null;
    }

    public void AtualizarValorAtual(decimal valorAtual)
    {
        if (valorAtual < 0)
            throw new ArgumentException("Valor atual não pode ser negativo.", nameof(valorAtual));

        if (Encerrado)
            throw new InvalidOperationException("Não é possível atualizar um investimento encerrado.");

        ValorAtual = decimal.Round(valorAtual, 2);
    }

    public void Encerrar(decimal valorResgate)
    {
        if (Encerrado)
            throw new InvalidOperationException("Investimento já está encerrado.");

        if (valorResgate < 0)
            throw new ArgumentException("Valor de resgate não pode ser negativo.", nameof(valorResgate));

        ValorAtual = decimal.Round(valorResgate, 2);
        Encerrado = true;
    }
}
