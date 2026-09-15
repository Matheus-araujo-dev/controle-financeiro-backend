using System.Text.Json;
using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Conciliacao;

public sealed class MemoriaEstabelecimento : TenantEntity
{
    private MemoriaEstabelecimento() { }
    public Guid CartaoId { get; private set; }
    public string Chave { get; private set; } = string.Empty;
    public string PreferenciasJson { get; private set; } = "{}";
    public int Confirmacoes { get; private set; }

    public static MemoriaEstabelecimento Criar(Guid cartaoId, string chave)
    {
        if (cartaoId == Guid.Empty || string.IsNullOrWhiteSpace(chave) || chave.Length > 500)
            throw new ArgumentException("Cartão e estabelecimento são obrigatórios.");
        return new() { CartaoId = cartaoId, Chave = chave.Trim() };
    }

    public void AplicarDecisao(IReadOnlyDictionary<string, string> campos)
    {
        if (campos.Count == 0) throw new ArgumentException("Informe ao menos um campo confirmado.");
        var merged = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(PreferenciasJson)!;
        try
        {
            foreach (var (campo, value) in campos)
            {
                if (string.IsNullOrWhiteSpace(campo) || campo.Length > 80) throw new ArgumentException("Campo inválido.");
                using var parsed = JsonDocument.Parse(value);
                merged[campo] = parsed.RootElement.Clone();
            }
        }
        catch (JsonException ex) { throw new ArgumentException("Preferência inválida.", nameof(campos), ex); }
        var json = JsonSerializer.Serialize(merged);
        if (json.Length > 65536) throw new ArgumentException("Preferências excedem o limite permitido.");
        PreferenciasJson = json;
        Confirmacoes++;
    }
}
