using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Financeiro;

public sealed class Plano : TenantEntity
{
    private Plano()
    {
    }

    public string Nome { get; private set; } = string.Empty;

    public string? Descricao { get; private set; }

    public decimal ValorMensal { get; private set; }

    public int NumParcelas { get; private set; }

    public Guid ContaBancariaCaixaId { get; private set; }

    public Guid? FormaPagamentoId { get; private set; }

    public Guid? RecebedorId { get; private set; }

    public Guid? ContaGerencialId { get; private set; }

    public int ParcelasPagas { get; private set; }

    public decimal TotalRetirado { get; private set; }

    public bool Cancelado { get; private set; }

    public decimal ValorTotal => ValorMensal * NumParcelas;

    public decimal TotalAcumulado => (ValorMensal * ParcelasPagas) - TotalRetirado;

    public bool Concluido => ParcelasPagas >= NumParcelas;

    public static Plano Criar(
        string nome,
        string? descricao,
        decimal valorMensal,
        int numParcelas,
        Guid contaBancariaCaixaId)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome do plano é obrigatório.", nameof(nome));

        if (valorMensal <= 0)
            throw new ArgumentException("Valor mensal deve ser maior que zero.", nameof(valorMensal));

        if (numParcelas <= 0)
            throw new ArgumentException("Número de parcelas deve ser maior que zero.", nameof(numParcelas));

        if (contaBancariaCaixaId == Guid.Empty)
            throw new ArgumentException("Conta bancária caixa é obrigatória.", nameof(contaBancariaCaixaId));

        return new Plano
        {
            Nome = nome.Trim(),
            Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim(),
            ValorMensal = decimal.Round(valorMensal, 2, MidpointRounding.AwayFromZero),
            NumParcelas = numParcelas,
            ContaBancariaCaixaId = contaBancariaCaixaId
        };
    }

    public void ConfigurarContaPagar(Guid? formaPagamentoId, Guid? recebedorId, Guid? contaGerencialId)
    {
        FormaPagamentoId = formaPagamentoId;
        RecebedorId = recebedorId;
        ContaGerencialId = contaGerencialId;
    }

    public void Atualizar(string nome, string? descricao, decimal valorMensal, int numParcelas)
    {
        if (Cancelado)
            throw new InvalidOperationException("Plano cancelado não pode ser alterado.");

        if (Concluido)
            throw new InvalidOperationException("Plano concluído não pode ser alterado.");

        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome do plano é obrigatório.", nameof(nome));

        if (valorMensal <= 0)
            throw new ArgumentException("Valor mensal deve ser maior que zero.", nameof(valorMensal));

        if (numParcelas < ParcelasPagas)
            throw new ArgumentException("Número de parcelas não pode ser menor que as parcelas já pagas.", nameof(numParcelas));

        Nome = nome.Trim();
        Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        ValorMensal = decimal.Round(valorMensal, 2, MidpointRounding.AwayFromZero);
        NumParcelas = numParcelas;
    }

    public void AdiantarParcela()
    {
        if (Cancelado)
            throw new InvalidOperationException("Plano cancelado não aceita novas parcelas.");

        if (Concluido)
            throw new InvalidOperationException("Plano já está concluído.");

        ParcelasPagas++;
    }

    public void RetirarDinheiro(decimal valor)
    {
        if (Cancelado)
            throw new InvalidOperationException("Plano cancelado não aceita retiradas.");

        if (valor <= 0)
            throw new ArgumentException("Valor de retirada deve ser maior que zero.", nameof(valor));

        var valorArredondado = decimal.Round(valor, 2, MidpointRounding.AwayFromZero);

        if (valorArredondado > TotalAcumulado)
            throw new InvalidOperationException("Valor de retirada excede o total acumulado no plano.");

        TotalRetirado += valorArredondado;
    }

    public void Cancelar()
    {
        if (Cancelado)
            throw new InvalidOperationException("Plano já está cancelado.");

        Cancelado = true;
    }
}
