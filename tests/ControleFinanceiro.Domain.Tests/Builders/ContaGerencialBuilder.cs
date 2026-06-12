using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;

namespace ControleFinanceiro.Domain.Tests.Builders;

public sealed class ContaGerencialBuilder
{
    private string? _codigo = "COD-001";
    private string _descricao = "Conta Gerencial Teste";
    private TipoContaGerencial _tipo = TipoContaGerencial.Despesa;
    private Guid? _contaPaiId;
    private Guid? _responsavelPadraoId;
    private bool _ativo = true;
    private bool _ehPadraoRecebimentoFaturaCartao;

    public ContaGerencialBuilder ComCodigo(string? codigo)
    {
        _codigo = codigo;
        return this;
    }

    public ContaGerencialBuilder ComDescricao(string descricao)
    {
        _descricao = descricao;
        return this;
    }

    public ContaGerencialBuilder ComTipo(TipoContaGerencial tipo)
    {
        _tipo = tipo;
        return this;
    }

    public ContaGerencialBuilder Receita()
    {
        _tipo = TipoContaGerencial.Receita;
        return this;
    }

    public ContaGerencialBuilder Despesa()
    {
        _tipo = TipoContaGerencial.Despesa;
        return this;
    }

    public ContaGerencialBuilder ComContaPaiId(Guid? contaPaiId)
    {
        _contaPaiId = contaPaiId;
        return this;
    }

    public ContaGerencialBuilder ComResponsavelPadraoId(Guid? responsavelPadraoId)
    {
        _responsavelPadraoId = responsavelPadraoId;
        return this;
    }

    public ContaGerencialBuilder Ativo()
    {
        _ativo = true;
        return this;
    }

    public ContaGerencialBuilder Inativo()
    {
        _ativo = false;
        return this;
    }

    public ContaGerencialBuilder EhPadraoRecebimentoFaturaCartao()
    {
        _ehPadraoRecebimentoFaturaCartao = true;
        return this;
    }

    public ContaGerencial Build()
    {
        return ContaGerencial.Criar(
            _codigo,
            _descricao,
            _tipo,
            _contaPaiId,
            _responsavelPadraoId,
            _ativo,
            _ehPadraoRecebimentoFaturaCartao);
    }
}