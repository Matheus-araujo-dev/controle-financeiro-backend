using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Cadastros.ContasGerenciais;

public enum TipoContaGerencial
{
    Receita = 1,
    Despesa = 2
}

public sealed class ContaGerencial : AuditableEntity
{
    private ContaGerencial()
    {
    }

    public string? Codigo { get; private set; }

    public string Descricao { get; private set; } = string.Empty;

    public TipoContaGerencial Tipo { get; private set; }

    public Guid? ContaPaiId { get; private set; }

    public bool Ativo { get; private set; }

    public static ContaGerencial Criar(
        string? codigo,
        string descricao,
        TipoContaGerencial tipo,
        Guid? contaPaiId,
        bool ativo)
    {
        var conta = new ContaGerencial();
        conta.Atualizar(codigo, descricao, tipo, contaPaiId, ativo);
        return conta;
    }

    public void Atualizar(
        string? codigo,
        string descricao,
        TipoContaGerencial tipo,
        Guid? contaPaiId,
        bool ativo)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            throw new ArgumentException("Descricao e obrigatoria.", nameof(descricao));
        }

        Codigo = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim();
        Descricao = descricao.Trim();
        Tipo = tipo;
        AtualizarContaPai(contaPaiId);
        Ativo = ativo;
    }

    public void AtualizarContaPai(Guid? contaPaiId)
    {
        if (contaPaiId.HasValue && contaPaiId.Value == Id)
        {
            throw new ArgumentException("Conta pai nao pode ser a propria conta.", nameof(contaPaiId));
        }

        ContaPaiId = contaPaiId;
    }
}
