using System.ComponentModel.DataAnnotations;

namespace ControleFinanceiro.Contracts.Bootstrap;

public sealed record BootstrapEchoRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(60, ErrorMessage = "Name cannot exceed 60 characters.")]
    public string Name { get; init; } = string.Empty;
}
