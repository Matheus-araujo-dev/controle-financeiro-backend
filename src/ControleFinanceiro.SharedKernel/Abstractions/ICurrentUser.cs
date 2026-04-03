namespace ControleFinanceiro.SharedKernel.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
}
