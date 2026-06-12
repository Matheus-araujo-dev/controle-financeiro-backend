using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ControleFinanceiro.Application.Identidade;
using ControleFinanceiro.Domain.Identidade;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ControleFinanceiro.Infrastructure.Identity;

public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : ITokenService
{
    public const string FamiliaClaim = "familiaId";
    public const string PapelClaim = "papel";

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(options.Value.RefreshTokenDays);

    public AccessTokenResult CreateAccessToken(Usuario usuario, Guid familiaId, PapelFamilia papel)
    {
        var jwtOptions = options.Value;

        if (string.IsNullOrWhiteSpace(jwtOptions.JwtSigningKey))
        {
            throw new InvalidOperationException("Auth:JwtSigningKey não está configurada.");
        }

        var utcNow = clock.UtcNow;
        var expiresAtUtc = utcNow.AddMinutes(jwtOptions.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new(JwtRegisteredClaimNames.Name, usuario.Nome),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(FamiliaClaim, familiaId.ToString()),
            new(PapelClaim, papel.ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.JwtSigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtOptions.JwtIssuer,
            audience: jwtOptions.JwtAudience,
            claims: claims,
            notBefore: utcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }

    public string GenerateOpaqueToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}
