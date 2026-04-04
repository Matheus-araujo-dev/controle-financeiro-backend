using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Cadastros.FormasPagamento;

public enum TipoFormaPagamento
{
    Dinheiro = 1,
    Pix = 2,
    Boleto = 3,
    Transferencia = 4,
    Debito = 5,
    Credito = 6,
    Outro = 7
}

public sealed class FormaPagamento : AuditableEntity
{
    private FormaPagamento()
    {
    }

    public string Nome { get; private set; } = string.Empty;

    public TipoFormaPagamento Tipo { get; private set; }

    public bool EhCartao { get; private set; }

    public bool BaixarAutomaticamente { get; private set; }

    public bool Ativo { get; private set; }

    public static FormaPagamento Criar(
        string nome,
        TipoFormaPagamento tipo,
        bool ehCartao,
        bool baixarAutomaticamente,
        bool ativo)
    {
        var formaPagamento = new FormaPagamento();
        formaPagamento.Atualizar(nome, tipo, ehCartao, baixarAutomaticamente, ativo);
        return formaPagamento;
    }

    public void Atualizar(
        string nome,
        TipoFormaPagamento tipo,
        bool ehCartao,
        bool baixarAutomaticamente,
        bool ativo)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome e obrigatorio.", nameof(nome));
        }

        Nome = nome.Trim();
        Tipo = tipo;
        EhCartao = ehCartao;
        BaixarAutomaticamente = baixarAutomaticamente;
        Ativo = ativo;
    }
}
