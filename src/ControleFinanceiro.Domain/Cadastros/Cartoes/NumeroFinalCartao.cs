namespace ControleFinanceiro.Domain.Cadastros.Cartoes;

/// <summary>
/// Value object que representa os últimos dígitos de um cartão (máx. 4 dígitos numéricos).
/// </summary>
public readonly struct NumeroFinalCartao : IEquatable<NumeroFinalCartao>
{
    public string Valor { get; }

    private NumeroFinalCartao(string valor)
    {
        Valor = valor;
    }

    public static NumeroFinalCartao De(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException("Número final do cartão não pode ser vazio.", nameof(valor));

        var trimmed = valor.Trim();

        if (trimmed.Length > 4)
            throw new ArgumentException("Número final do cartão deve ter no máximo 4 dígitos.", nameof(valor));

        if (!trimmed.All(char.IsDigit))
            throw new ArgumentException("Número final do cartão deve conter apenas dígitos.", nameof(valor));

        return new NumeroFinalCartao(trimmed);
    }

    public static NumeroFinalCartao? TentarDe(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        try { return De(valor); }
        catch (ArgumentException) { return null; }
    }

    public static implicit operator string(NumeroFinalCartao n) => n.Valor;

    public bool Equals(NumeroFinalCartao other) => Valor == other.Valor;
    public override bool Equals(object? obj) => obj is NumeroFinalCartao other && Equals(other);
    public override int GetHashCode() => Valor.GetHashCode(StringComparison.Ordinal);
    public override string ToString() => $"**** {Valor}";

    public static bool operator ==(NumeroFinalCartao left, NumeroFinalCartao right) => left.Equals(right);
    public static bool operator !=(NumeroFinalCartao left, NumeroFinalCartao right) => !left.Equals(right);
}
