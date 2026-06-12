using System.ComponentModel.DataAnnotations;

namespace ControleFinanceiro.Contracts.Familias;

public sealed record MembroFamiliaResponse(
    Guid Id,
    Guid UsuarioId,
    string Nome,
    string Email,
    string? AvatarUrl,
    string Papel);

public sealed record ConviteFamiliaResponse(
    Guid Id,
    string EmailConvidado,
    string Papel,
    string Status,
    DateTime ExpiraEmUtc);

public sealed record FamiliaDetalheResponse(
    Guid Id,
    string Nome,
    string MeuPapel,
    IReadOnlyList<MembroFamiliaResponse> Membros,
    IReadOnlyList<ConviteFamiliaResponse> ConvitesPendentes);

public sealed record CriarConviteFamiliaRequest(
    [Required][EmailAddress] string Email,
    [Required] string Papel);

public sealed record ConviteCriadoResponse(
    Guid Id,
    string EmailConvidado,
    string Papel,
    DateTime ExpiraEmUtc,
    string Token);

public sealed record ConviteDetalhePublicoResponse(
    string NomeFamilia,
    string EmailConvidado,
    string Papel,
    bool Valido);

public sealed record AlterarPapelMembroRequest([Required] string Papel);

public sealed record RenomearFamiliaRequest([Required] string Nome);
