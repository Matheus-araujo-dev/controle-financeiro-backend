using ControleFinanceiro.Domain.Cadastros.Pessoas;

namespace ControleFinanceiro.Domain.Tests.Builders;

public sealed class PessoaBuilder
{
    private string _nome = "Pessoa Teste";
    private TipoPessoa _tipoPessoa = TipoPessoa.Fisica;
    private string? _cpfCnpj = "12345678901";
    private string? _email = "teste@exemplo.com";
    private string? _telefone = "11999999999";
    private string? _observacao = "Observação de teste";
    private bool _ativo = true;
    private List<ChavePixPlano> _chavesPix = [];

    public PessoaBuilder ComNome(string nome)
    {
        _nome = nome;
        return this;
    }

    public PessoaBuilder ComTipoPessoa(TipoPessoa tipoPessoa)
    {
        _tipoPessoa = tipoPessoa;
        return this;
    }

    public PessoaBuilder ComCpfCnpj(string? cpfCnpj)
    {
        _cpfCnpj = cpfCnpj;
        return this;
    }

    public PessoaBuilder ComEmail(string? email)
    {
        _email = email;
        return this;
    }

    public PessoaBuilder ComTelefone(string? telefone)
    {
        _telefone = telefone;
        return this;
    }

    public PessoaBuilder ComObservacao(string? observacao)
    {
        _observacao = observacao;
        return this;
    }

    public PessoaBuilder Ativo()
    {
        _ativo = true;
        return this;
    }

    public PessoaBuilder Inativo()
    {
        _ativo = false;
        return this;
    }

    public PessoaBuilder ComChavesPix(params ChavePixPlano[] chavesPix)
    {
        _chavesPix = [.. chavesPix];
        return this;
    }

    public Pessoa Build()
    {
        return Pessoa.Criar(
            _nome,
            _tipoPessoa,
            _cpfCnpj,
            _email,
            _telefone,
            _observacao,
            _chavesPix,
            _ativo);
    }
}