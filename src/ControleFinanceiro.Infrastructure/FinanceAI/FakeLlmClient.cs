using ControleFinanceiro.Application.FinanceAI;

namespace ControleFinanceiro.Infrastructure.FinanceAI;

/// <summary>
/// Implementação determinística para testes — nunca chama a API real.
/// Respostas configuráveis via <see cref="Queue"/>.
/// </summary>
public sealed class FakeLlmClient : ILlmClient, ILlmVisionClient
{
    private readonly Queue<LlmCompletion> _queue = new();

    public List<LlmRequest> Requests { get; } = [];

    public void Enqueue(LlmCompletion completion) => _queue.Enqueue(completion);

    public void EnqueueText(string text) =>
        _queue.Enqueue(new LlmCompletion(text, [], new LlmUsage(10, 10), "end_turn"));

    public void EnqueueToolCall(string toolName, string inputJson) =>
        _queue.Enqueue(new LlmCompletion(null,
            [new LlmToolCall(toolName, inputJson, Guid.NewGuid().ToString())],
            new LlmUsage(10, 10), "tool_use"));

    public Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (_queue.TryDequeue(out var completion))
            return Task.FromResult(completion);

        return Task.FromResult(new LlmCompletion(
            "Resposta padrão do FakeLlmClient.", [], new LlmUsage(0, 0), "end_turn"));
    }

    public Task<string?> AnalisarImagemAsync(
        string systemPrompt, string userText, string imagemBase64, string mimeType, CancellationToken cancellationToken)
        => Task.FromResult<string?>("""{"sucesso": false}""");
}
