namespace ControleFinanceiro.Domain.Cadastros.Pessoas;

public sealed record ChavePixPlano(TipoChavePix Tipo, string Chave)
{
    public static ChavePixPlano Create(TipoChavePix tipo, string chave)
    {
        if (string.IsNullOrWhiteSpace(chave))
        {
            throw new ArgumentException("Chave Pix e obrigatoria.", nameof(chave));
        }

        var chaveNormalizada = tipo switch
        {
            TipoChavePix.CpfCnpj => NormalizarDigitos(chave),
            TipoChavePix.Telefone => NormalizarDigitos(chave),
            TipoChavePix.Email => chave.Trim().ToLowerInvariant(),
            TipoChavePix.Aleatoria => chave.Trim().ToLowerInvariant(),
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };

        if (string.IsNullOrWhiteSpace(chaveNormalizada))
        {
            throw new ArgumentException("Chave Pix e obrigatoria.", nameof(chave));
        }

        if (chaveNormalizada.Length > 120)
        {
            throw new ArgumentException("Chave Pix excede o limite de 120 caracteres.", nameof(chave));
        }

        return new ChavePixPlano(tipo, chaveNormalizada);
    }

    private static string NormalizarDigitos(string chave)
    {
        return new string(chave.Where(char.IsDigit).ToArray());
    }
}
