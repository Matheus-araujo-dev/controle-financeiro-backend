using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Financeiro;

public sealed class Transferencia : TenantEntity
{
    private Transferencia()
    {
    }

    public Guid ContaBancariaOrigemId { get; private set; }

    public Guid ContaBancariaDestinoId { get; private set; }

    public decimal Valor { get; private set; }

    public DateOnly DataTransferencia { get; private set; }

    public string? Descricao { get; private set; }

    public bool Cancelada { get; private set; }

    public static Transferencia Criar(
        Guid contaBancariaOrigemId,
        Guid contaBancariaDestinoId,
        decimal valor,
        DateOnly dataTransferencia,
        string? descricao)
    {
        if (contaBancariaOrigemId == contaBancariaDestinoId)
            throw new ArgumentException("Conta de origem e destino nao podem ser iguais.");

        if (valor <= 0)
            throw new ArgumentException("Valor da transferencia deve ser maior que zero.", nameof(valor));

        return new Transferencia
        {
            ContaBancariaOrigemId = contaBancariaOrigemId,
            ContaBancariaDestinoId = contaBancariaDestinoId,
            Valor = decimal.Round(valor, 2, MidpointRounding.AwayFromZero),
            DataTransferencia = dataTransferencia,
            Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim()
        };
    }

    public void Cancelar()
    {
        if (Cancelada)
            throw new InvalidOperationException("Transferencia ja esta cancelada.");

        Cancelada = true;
    }
}
