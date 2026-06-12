using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Identidade;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Infrastructure.Identity;

public sealed class GoogleTokenValidator(IOptions<JwtOptions> options) : IGoogleTokenValidator
{
    public async Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        var clientId = options.Value.GoogleClientId;

        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("Auth:GoogleClientId não está configurado.");
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [clientId]
                });

            return new GoogleUserInfo(
                payload.Subject,
                payload.Email,
                payload.Name ?? payload.Email,
                payload.Picture);
        }
        catch (InvalidJwtException exception)
        {
            throw new AuthenticationFailedException($"Token Google inválido: {exception.Message}");
        }
    }
}
