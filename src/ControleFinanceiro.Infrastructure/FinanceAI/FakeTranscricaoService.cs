using ControleFinanceiro.Application.FinanceAI;

namespace ControleFinanceiro.Infrastructure.FinanceAI;

/// <summary>Fallback quando OpenAi:ApiKey não está configurado.</summary>
public sealed class FakeTranscricaoService : ITranscricaoAudioService
{
    public Task<string?> TranscreverAsync(string midiaBase64, string mimeType, CancellationToken cancellationToken)
        => Task.FromResult<string?>(null);
}
