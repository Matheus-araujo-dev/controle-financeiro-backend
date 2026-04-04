using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Cadastros.Cartoes;

public sealed class Cartao : AuditableEntity
{
    private Cartao()
    {
    }

    public string Nome { get; private set; } = string.Empty;

    public string Bandeira { get; private set; } = string.Empty;

    public string NumeroFinal { get; private set; } = string.Empty;

    public int DiaFechamentoFatura { get; private set; }

    public int DiaVencimentoFatura { get; private set; }

    public Guid? ContaBancariaPagamentoPadraoId { get; private set; }

    public decimal? LimiteCredito { get; private set; }

    public bool Ativo { get; private set; }

    public static Cartao Criar(
        string nome,
        string bandeira,
        string numeroFinal,
        int diaFechamentoFatura,
        int diaVencimentoFatura,
        Guid? contaBancariaPagamentoPadraoId,
        decimal? limiteCredito,
        bool ativo)
    {
        var cartao = new Cartao();
        cartao.Atualizar(
            nome,
            bandeira,
            numeroFinal,
            diaFechamentoFatura,
            diaVencimentoFatura,
            contaBancariaPagamentoPadraoId,
            limiteCredito,
            ativo);

        return cartao;
    }

    public void Atualizar(
        string nome,
        string bandeira,
        string numeroFinal,
        int diaFechamentoFatura,
        int diaVencimentoFatura,
        Guid? contaBancariaPagamentoPadraoId,
        decimal? limiteCredito,
        bool ativo)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome e obrigatorio.", nameof(nome));
        }

        if (string.IsNullOrWhiteSpace(bandeira))
        {
            throw new ArgumentException("Bandeira e obrigatoria.", nameof(bandeira));
        }

        if (string.IsNullOrWhiteSpace(numeroFinal) || numeroFinal.Length != 4 || numeroFinal.Any(ch => !char.IsDigit(ch)))
        {
            throw new ArgumentException("Numero final deve possuir exatamente 4 digitos.", nameof(numeroFinal));
        }

        if (diaFechamentoFatura < 1 || diaFechamentoFatura > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(diaFechamentoFatura), "Dia de fechamento deve ficar entre 1 e 31.");
        }

        if (diaVencimentoFatura < 1 || diaVencimentoFatura > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(diaVencimentoFatura), "Dia de vencimento deve ficar entre 1 e 31.");
        }

        Nome = nome.Trim();
        Bandeira = bandeira.Trim();
        NumeroFinal = numeroFinal;
        DiaFechamentoFatura = diaFechamentoFatura;
        DiaVencimentoFatura = diaVencimentoFatura;
        ContaBancariaPagamentoPadraoId = contaBancariaPagamentoPadraoId;
        LimiteCredito = limiteCredito.HasValue
            ? decimal.Round(limiteCredito.Value, 2, MidpointRounding.AwayFromZero)
            : null;
        Ativo = ativo;
    }
}
