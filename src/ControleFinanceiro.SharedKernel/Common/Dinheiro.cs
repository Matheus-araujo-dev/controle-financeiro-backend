using System.Globalization;

namespace ControleFinanceiro.SharedKernel.Common;

/// <summary>
/// Value object que representa um valor monetário não-negativo em BRL.
/// Garante escala de 2 casas decimais e semântica de igualdade por valor.
/// </summary>
public readonly struct Dinheiro : IEquatable<Dinheiro>, IComparable<Dinheiro>
{
    public static readonly Dinheiro Zero = new(0m);

    public decimal Valor { get; }

    private Dinheiro(decimal valor)
    {
        Valor = valor;
    }

    public static Dinheiro De(decimal valor)
    {
        if (valor < 0)
            throw new ArgumentOutOfRangeException(nameof(valor), "Valor monetário não pode ser negativo.");
        return new Dinheiro(decimal.Round(valor, 2, MidpointRounding.AwayFromZero));
    }

    public static Dinheiro DePermitindoNegativo(decimal valor) =>
        new(decimal.Round(valor, 2, MidpointRounding.AwayFromZero));

    public static implicit operator decimal(Dinheiro d) => d.Valor;

    public static Dinheiro operator +(Dinheiro a, Dinheiro b) =>
        new(a.Valor + b.Valor);

    public static Dinheiro operator -(Dinheiro a, Dinheiro b) =>
        DePermitindoNegativo(a.Valor - b.Valor);

    public static Dinheiro operator *(Dinheiro d, decimal fator) =>
        new(decimal.Round(d.Valor * fator, 2, MidpointRounding.AwayFromZero));

    public static bool operator ==(Dinheiro left, Dinheiro right) => left.Equals(right);
    public static bool operator !=(Dinheiro left, Dinheiro right) => !left.Equals(right);
    public static bool operator <(Dinheiro left, Dinheiro right) => left.CompareTo(right) < 0;
    public static bool operator >(Dinheiro left, Dinheiro right) => left.CompareTo(right) > 0;
    public static bool operator <=(Dinheiro left, Dinheiro right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Dinheiro left, Dinheiro right) => left.CompareTo(right) >= 0;

    public bool Equals(Dinheiro other) => Valor == other.Valor;
    public override bool Equals(object? obj) => obj is Dinheiro other && Equals(other);
    public override int GetHashCode() => Valor.GetHashCode();
    public int CompareTo(Dinheiro other) => Valor.CompareTo(other.Valor);
    public override string ToString() => Valor.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
}
