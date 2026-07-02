namespace ControleFinanceiro.SharedKernel.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
    Guid? WorkspaceId { get; }
    Guid? FamiliaId { get; }
    string? Papel { get; }
}
