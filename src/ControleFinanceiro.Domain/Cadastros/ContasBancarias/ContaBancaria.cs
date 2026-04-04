using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Cadastros.ContasBancarias;

public sealed class ContaBancaria : AuditableEntity
{
    private ContaBancaria()
    {
    }

    public string Nome { get; private set; } = string.Empty;

    public string Banco { get; private set; } = string.Empty;

    public string? Agencia { get; private set; }

    public string? NumeroConta { get; private set; }

    public string? TipoConta { get; private set; }

    public decimal SaldoInicial { get; private set; }

    public DateOnly DataSaldoInicial { get; private set; }

    public bool Ativo { get; private set; }

    public static ContaBancaria Criar(
        string nome,
        string banco,
        string? agencia,
        string? numeroConta,
        string? tipoConta,
        decimal saldoInicial,
        DateOnly dataSaldoInicial,
        bool ativo)
    {
        var conta = new ContaBancaria();
        conta.Atualizar(nome, banco, agencia, numeroConta, tipoConta, saldoInicial, dataSaldoInicial, ativo);
        return conta;
    }

    public void Atualizar(
        string nome,
        string banco,
        string? agencia,
        string? numeroConta,
        string? tipoConta,
        decimal saldoInicial,
        DateOnly dataSaldoInicial,
        bool ativo)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome e obrigatorio.", nameof(nome));
        }

        if (string.IsNullOrWhiteSpace(banco))
        {
            throw new ArgumentException("Banco e obrigatorio.", nameof(banco));
        }

        Nome = nome.Trim();
        Banco = banco.Trim();
        Agencia = NormalizarOpcional(agencia);
        NumeroConta = NormalizarOpcional(numeroConta);
        TipoConta = NormalizarOpcional(tipoConta);
        SaldoInicial = decimal.Round(saldoInicial, 2, MidpointRounding.AwayFromZero);
        DataSaldoInicial = dataSaldoInicial;
        Ativo = ativo;
    }

    private static string? NormalizarOpcional(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }
}
