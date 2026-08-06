using System.Text.Json;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Financeiro.Common;

namespace ControleFinanceiro.Application.Financeiro.Common;

public static class HistoricoMapper
{
    private static readonly IReadOnlyDictionary<string, string> NomesCampos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["NumeroDocumento"] = "Nº Documento",
        ["DataEmissao"] = "Data de Emissão",
        ["DataVencimento"] = "Vencimento",
        ["DataLiquidacao"] = "Data de Liquidação",
        ["DataCompra"] = "Data da Compra",
        ["ValorOriginal"] = "Valor Original",
        ["ValorDesconto"] = "Desconto",
        ["ValorJuros"] = "Juros",
        ["ValorMulta"] = "Multa",
        ["ValorLiquido"] = "Valor Líquido",
        ["ValorPago"] = "Valor Pago",
        ["QuantidadeParcelas"] = "Parcelas",
        ["NumeroParcela"] = "Nº Parcela",
        ["Descricao"] = "Descrição",
        ["Observacao"] = "Observações",
        ["EhRecorrente"] = "Recorrente",
    };

    private static readonly IReadOnlySet<string> CamposIgnorados = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "FamiliaId", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy",
        "RecebedorId", "PagadorId", "ResponsavelCompraId", "ResponsavelId",
        "FormaPagamentoId", "CartaoId", "ContaBancariaId", "StatusContaId",
        "RegraRecorrenciaId", "GrupoParcelamentoId", "GrupoReembolsoId", "GrupoResponsaveisId",
        "OrigemCompraPlanejadaId", "ContaVinculadaId",
    };

    private static readonly IReadOnlySet<string> CamposDecimais = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ValorOriginal", "ValorDesconto", "ValorJuros", "ValorMulta", "ValorLiquido", "ValorPago",
    };

    public static IReadOnlyList<HistoricoEntradaResponse> Mapear(IReadOnlyList<AuditEntryDto> entries)
        => entries.Select(Mapear).ToList();

    public static HistoricoEntradaResponse Mapear(AuditEntryDto entry)
    {
        var acao = InferirAcao(entry);
        var alteracoes = ExtrairAlteracoes(entry);
        var regraId = ExtrairRegraRecorrenciaId(entry);

        var realizadoPor = string.IsNullOrWhiteSpace(entry.ExecutedBy) || entry.ExecutedBy == "system"
            ? (regraId.HasValue ? "Recorrência automática" : "Sistema")
            : entry.ExecutedBy;

        return new HistoricoEntradaResponse(
            entry.Id,
            acao,
            realizadoPor,
            entry.OccurredAtUtc,
            alteracoes,
            regraId);
    }

    private static string InferirAcao(AuditEntryDto entry)
    {
        if (entry.Action == "Created") return "Criação";
        if (entry.Action == "Deleted") return "Exclusão";

        if (entry.BeforeJson != null && entry.AfterJson != null)
        {
            using var before = JsonDocument.Parse(entry.BeforeJson);
            using var after = JsonDocument.Parse(entry.AfterJson);

            var beforeLiq = GetStringValue(before.RootElement, "DataLiquidacao");
            var afterLiq = GetStringValue(after.RootElement, "DataLiquidacao");

            if (string.IsNullOrEmpty(beforeLiq) && !string.IsNullOrEmpty(afterLiq))
                return "Liquidação";

            if (!string.IsNullOrEmpty(beforeLiq) && string.IsNullOrEmpty(afterLiq))
                return "Estorno";
        }

        return "Edição";
    }

    private static IReadOnlyList<AlteracaoCampoResponse> ExtrairAlteracoes(AuditEntryDto entry)
    {
        if (entry.AfterJson == null)
            return [];

        var result = new List<AlteracaoCampoResponse>();

        using var after = JsonDocument.Parse(entry.AfterJson);
        var before = entry.BeforeJson != null ? JsonDocument.Parse(entry.BeforeJson) : null;
        var isCriacao = entry.Action == "Created";

        try
        {
            foreach (var prop in after.RootElement.EnumerateObject())
            {
                if (CamposIgnorados.Contains(prop.Name)) continue;
                if (!NomesCampos.TryGetValue(prop.Name, out var label)) continue;

                var afterVal = FormatarValor(prop.Name, prop.Value);

                if (isCriacao)
                {
                    if (afterVal != null)
                        result.Add(new AlteracaoCampoResponse(label, null, afterVal));
                    continue;
                }

                var beforeVal = before != null && before.RootElement.TryGetProperty(prop.Name, out var bv)
                    ? FormatarValor(prop.Name, bv)
                    : null;

                if (beforeVal != afterVal)
                    result.Add(new AlteracaoCampoResponse(label, beforeVal, afterVal));
            }
        }
        finally
        {
            before?.Dispose();
        }

        return result;
    }

    private static Guid? ExtrairRegraRecorrenciaId(AuditEntryDto entry)
    {
        var json = entry.AfterJson ?? entry.BeforeJson;
        if (json == null) return null;

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("RegraRecorrenciaId", out var prop)) return null;
        if (prop.ValueKind == JsonValueKind.Null) return null;

        return prop.TryGetGuid(out var id) ? id : null;
    }

    private static string? FormatarValor(string propName, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null) return null;

        if (CamposDecimais.Contains(propName))
        {
            if (element.TryGetDecimal(out var dec))
                return dec.ToString("C2", new System.Globalization.CultureInfo("pt-BR"));
        }

        var raw = element.ToString();
        if (string.IsNullOrEmpty(raw)) return null;

        // Tentar formatar datas no padrão br
        if (DateTime.TryParse(raw, out var dt))
            return dt.ToString("dd/MM/yyyy");

        if (raw == "True") return "Sim";
        if (raw == "False") return "Não";

        return raw;
    }

    private static string? GetStringValue(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop)) return null;
        return prop.ValueKind == JsonValueKind.Null ? null : prop.ToString();
    }
}
