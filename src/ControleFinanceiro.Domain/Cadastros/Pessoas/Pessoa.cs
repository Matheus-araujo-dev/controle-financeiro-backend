using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Cadastros.Pessoas;

public enum TipoPessoa
{
    Fisica = 1,
    Juridica = 2
}

public sealed class Pessoa : AuditableEntity
{
    private Pessoa()
    {
    }

    public string Nome { get; private set; } = string.Empty;

    public TipoPessoa TipoPessoa { get; private set; }

    public string? CpfCnpj { get; private set; }

    public string? Email { get; private set; }

    public string? Telefone { get; private set; }

    public string? Observacao { get; private set; }

    public bool Ativo { get; private set; }

    public static Pessoa Criar(
        string nome,
        TipoPessoa tipoPessoa,
        string? cpfCnpj,
        string? email,
        string? telefone,
        string? observacao,
        bool ativo)
    {
        var pessoa = new Pessoa();
        pessoa.Atualizar(nome, tipoPessoa, cpfCnpj, email, telefone, observacao, ativo);
        return pessoa;
    }

    public void Atualizar(
        string nome,
        TipoPessoa tipoPessoa,
        string? cpfCnpj,
        string? email,
        string? telefone,
        string? observacao,
        bool ativo)
    {
        Nome = NormalizarObrigatorio(nome, nameof(nome), 200);
        TipoPessoa = tipoPessoa;
        CpfCnpj = NormalizarDocumento(cpfCnpj);
        Email = NormalizarOpcional(email, 200);
        Telefone = NormalizarOpcional(telefone, 50);
        Observacao = NormalizarOpcional(observacao, 1000);
        Ativo = ativo;
    }

    public void Ativar()
    {
        Ativo = true;
    }

    public void Inativar()
    {
        Ativo = false;
    }

    private static string NormalizarObrigatorio(string? valor, string parametro, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ArgumentException("O valor informado e obrigatorio.", parametro);
        }

        var normalizado = valor.Trim();

        if (normalizado.Length > maximo)
        {
            throw new ArgumentException($"O valor informado excede o limite de {maximo} caracteres.", parametro);
        }

        return normalizado;
    }

    private static string? NormalizarOpcional(string? valor, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var normalizado = valor.Trim();

        if (normalizado.Length > maximo)
        {
            throw new ArgumentException($"O valor informado excede o limite de {maximo} caracteres.");
        }

        return normalizado;
    }

    private static string? NormalizarDocumento(string? cpfCnpj)
    {
        if (string.IsNullOrWhiteSpace(cpfCnpj))
        {
            return null;
        }

        var digitos = new string(cpfCnpj.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digitos) ? null : digitos;
    }
}
