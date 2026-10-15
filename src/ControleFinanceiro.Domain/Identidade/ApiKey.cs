using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Identidade;

public sealed class ApiKey : AuditableEntity
{
    private ApiKey()
    {
    }

    public string Nome { get; private set; } = string.Empty;

    public string KeyHash { get; private set; } = string.Empty;

    public string KeyPrefix { get; private set; } = string.Empty;

    public Guid FamiliaId { get; private set; }

    public string Papel { get; private set; } = "Agente";

    public bool Ativo { get; private set; }

    public DateTime? ExpiraEmUtc { get; private set; }

    public DateTime? UltimoUsoEmUtc { get; private set; }

    public static ApiKey Criar(string nome, string keyHash, string keyPrefix, Guid familiaId, string papel = "Agente", DateTime? expiraEmUtc = null)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome é obrigatório.", nameof(nome));

        if (string.IsNullOrWhiteSpace(keyHash))
            throw new ArgumentException("KeyHash é obrigatório.", nameof(keyHash));

        if (string.IsNullOrWhiteSpace(keyPrefix))
            throw new ArgumentException("KeyPrefix é obrigatório.", nameof(keyPrefix));

        if (familiaId == Guid.Empty)
            throw new ArgumentException("FamiliaId é obrigatório.", nameof(familiaId));

        return new ApiKey
        {
            Nome = nome.Trim(),
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            FamiliaId = familiaId,
            Papel = string.IsNullOrWhiteSpace(papel) ? "Agente" : papel.Trim(),
            Ativo = true,
            ExpiraEmUtc = expiraEmUtc
        };
    }

    public bool EstaValida(DateTime utcNow) =>
        Ativo && (ExpiraEmUtc is null || ExpiraEmUtc > utcNow);

    public void RegistrarUso(DateTime utcNow) =>
        UltimoUsoEmUtc = utcNow;

    public void Revogar() => Ativo = false;
}