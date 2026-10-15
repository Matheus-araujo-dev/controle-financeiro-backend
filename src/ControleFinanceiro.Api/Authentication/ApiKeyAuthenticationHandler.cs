using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Identidade;
using ControleFinanceiro.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Api.Authentication;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceScopeFactory scopeFactory)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValue) || string.IsNullOrWhiteSpace(headerValue))
        {
            return AuthenticateResult.NoResult();
        }

        var rawKey = headerValue.ToString().Trim();
        if (rawKey.Length < 32)
        {
            return AuthenticateResult.Fail("API key format is invalid.");
        }

        var keyHash = HashKey(rawKey);

        // Usar scope dedicado para evitar conflitos de lifetime com o DbContext do request
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var apiKey = await dbContext.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash, Context.RequestAborted);

        if (apiKey is null)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        if (!apiKey.EstaValida(DateTime.UtcNow))
        {
            return AuthenticateResult.Fail("API key is expired or revoked.");
        }

        // Atualizar último uso em background
        _ = Task.Run(async () =>
        {
            try
            {
                await using var bgScope = scopeFactory.CreateAsyncScope();
                var bgDb = bgScope.ServiceProvider.GetRequiredService<IAppDbContext>();
                var entity = await bgDb.ApiKeys.FindAsync(apiKey.Id);
                if (entity is not null)
                {
                    entity.RegistrarUso(DateTime.UtcNow);
                    await bgDb.SaveChangesAsync();
                }
            }
            catch
            {
                // Falha ao registrar uso não deve impedir a autenticação
            }
        });

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, apiKey.Id.ToString()),
            new Claim("sub", apiKey.Id.ToString()),
            new Claim(ClaimTypes.Name, apiKey.Nome),
            new Claim(JwtTokenService.WorkspaceClaim, apiKey.FamiliaId.ToString()),
            new Claim(JwtTokenService.FamiliaClaim, apiKey.FamiliaId.ToString()),
            new Claim(JwtTokenService.PapelClaim, apiKey.Papel),
            new Claim("auth_method", "api_key")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    internal static string HashKey(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash);
    }
}