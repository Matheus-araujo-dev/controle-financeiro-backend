using System.Globalization;
using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Financeiro;

public sealed class MetaOrcamento : TenantEntity
{
    private MetaOrcamento()
    {
    }

    public Guid ContaGerencialId { get; private set; }

    public string Competencia { get; private set; } = string.Empty;

    public decimal ValorMeta { get; private set; }

    public static MetaOrcamento Criar(Guid contaGerencialId, string competencia, decimal valorMeta)
    {
        if (contaGerencialId == Guid.Empty)
        {
            throw new ArgumentException("Conta gerencial é obrigatória.", nameof(contaGerencialId));
        }

        var meta = new MetaOrcamento
        {
            ContaGerencialId = contaGerencialId,
            Competencia = ValidarCompetencia(competencia)
        };

        meta.Atualizar(valorMeta);
        return meta;
    }

    public void Atualizar(decimal valorMeta)
    {
        if (valorMeta <= 0)
        {
            throw new ArgumentException("Valor da meta deve ser maior que zero.", nameof(valorMeta));
        }

        ValorMeta = decimal.Round(valorMeta, 2, MidpointRounding.AwayFromZero);
    }

    private static string ValidarCompetencia(string competencia)
    {
        if (string.IsNullOrWhiteSpace(competencia))
        {
            throw new ArgumentException("Competência é obrigatória.", nameof(competencia));
        }

        var valor = competencia.Trim();

        if (!DateOnly.TryParseExact(
                $"{valor}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            throw new ArgumentException("Competência inválida. Use o formato yyyy-MM.", nameof(competencia));
        }

        return valor;
    }
}
