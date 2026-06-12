using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Cadastros.ContasGerenciais;

public enum TipoContaGerencial
{
    Receita = 1,
    Despesa = 2
}

public sealed class ContaGerencial : TenantEntity
{
    private ContaGerencial()
    {
    }

    public string? Codigo { get; private set; }

    public string Descricao { get; private set; } = string.Empty;

    public TipoContaGerencial Tipo { get; private set; }

    public Guid? ContaPaiId { get; private set; }

    public Guid? ResponsavelPadraoId { get; private set; }

    public bool Ativo { get; private set; }

    public bool EhPadraoRecebimentoFaturaCartao { get; private set; }

    public static ContaGerencial Criar(
        string? codigo,
        string descricao,
        TipoContaGerencial tipo,
        Guid? contaPaiId,
        Guid? responsavelPadraoId,
        bool ativo,
        bool ehPadraoRecebimentoFaturaCartao)
    {
        var conta = new ContaGerencial();
        conta.Atualizar(codigo, descricao, tipo, contaPaiId, responsavelPadraoId, ativo, ehPadraoRecebimentoFaturaCartao);
        return conta;
    }

    public void Atualizar(
        string? codigo,
        string descricao,
        TipoContaGerencial tipo,
        Guid? contaPaiId,
        Guid? responsavelPadraoId,
        bool ativo,
        bool ehPadraoRecebimentoFaturaCartao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            throw new ArgumentException("Descrição é obrigatória.", nameof(descricao));
        }

        if (ehPadraoRecebimentoFaturaCartao && tipo != TipoContaGerencial.Receita)
        {
            throw new ArgumentException("Somente contas gerenciais de receita podem ser marcadas como padrão de recebimento de fatura.", nameof(ehPadraoRecebimentoFaturaCartao));
        }

        Codigo = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim();
        Descricao = descricao.Trim();
        Tipo = tipo;
        AtualizarContaPai(contaPaiId);
        ResponsavelPadraoId = responsavelPadraoId;
        Ativo = ativo;
        EhPadraoRecebimentoFaturaCartao = ehPadraoRecebimentoFaturaCartao;
    }

    public void AtualizarContaPai(Guid? contaPaiId)
    {
        if (contaPaiId.HasValue && contaPaiId.Value == Id)
        {
            throw new ArgumentException("Conta pai não pode ser a própria conta.", nameof(contaPaiId));
        }

        ContaPaiId = contaPaiId;
    }
}
