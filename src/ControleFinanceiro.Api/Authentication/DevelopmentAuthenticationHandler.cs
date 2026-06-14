using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using ControleFinanceiro.Api.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Api.Authentication;

public sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<AuthOptions> authOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Development";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headerName = authOptions.Value.DevelopmentUserHeader;

        if (!Request.Headers.TryGetValue(headerName, out var headerValue) || string.IsNullOrWhiteSpace(headerValue))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (headerValue.Count != 1)
        {
            return Task.FromResult(AuthenticateResult.Fail("Development user header must be sent only once."));
        }

        var rawUser = headerValue[0]?.Trim();
        if (string.IsNullOrWhiteSpace(rawUser))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = ResolverUsuarioId(rawUser);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString()),
            new Claim("userId", userId.ToString()),
            new Claim(ClaimTypes.Name, rawUser),
            new Claim("familiaId", authOptions.Value.DevelopmentFamiliaId.ToString()),
            new Claim("papel", "Administrador")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// Usuários técnicos de desenvolvimento normalmente são informados como texto livre
    /// (ex.: "matheus"). Endpoints que escopam por usuário exigem um <see cref="Guid"/>,
    /// então derivamos um identificador determinístico e estável a partir do nome.
    /// </summary>
    private static Guid ResolverUsuarioId(string rawUser)
    {
        if (Guid.TryParse(rawUser, out var parsed))
        {
            return parsed;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawUser));
        return new Guid(hash[..16]);
    }
}
