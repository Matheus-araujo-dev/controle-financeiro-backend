using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.FinanceAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Infrastructure.FinanceAI;

public sealed class AnthropicLlmClient(
    HttpClient httpClient,
    IOptions<LlmOptions> options,
    ILogger<AnthropicLlmClient> logger) : ILlmClient, ILlmVisionClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        var opts = options.Value;
        var model = request.Tier == LlmModelTier.Fast ? opts.FastModel : opts.ReasoningModel;

        var messages = request.Messages.Select(m => new
        {
            role = m.Role == LlmRole.Assistant ? "assistant" : "user",
            content = m.Content
        }).ToList();

        var body = new Dictionary<string, object>
        {
            ["model"] = model,
            ["max_tokens"] = opts.MaxTokens,
            ["system"] = request.SystemPrompt,
            ["messages"] = messages
        };

        if (request.Tools?.Count > 0)
        {
            body["tools"] = request.Tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                input_schema = JsonDocument.Parse(t.InputSchema).RootElement
            }).ToList();
        }

        var json = JsonSerializer.Serialize(body, JsonOpts);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        logger.LogDebug("Chamando Anthropic API modelo={Model} tier={Tier}", model, request.Tier);

        using var response = await httpClient.PostAsync("messages", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Anthropic API erro {Status}: {Body}", (int)response.StatusCode, error);
            throw new InvalidOperationException($"Anthropic API retornou {(int)response.StatusCode}: {error}");
        }

        var node = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Anthropic API retornou resposta vazia.");

        var usage = new LlmUsage(
            node["usage"]?["input_tokens"]?.GetValue<int>() ?? 0,
            node["usage"]?["output_tokens"]?.GetValue<int>() ?? 0);

        var stopReason = node["stop_reason"]?.GetValue<string>();
        var text = default(string);
        var toolCalls = new List<LlmToolCall>();

        var contentBlocks = node["content"]?.AsArray() ?? [];
        foreach (var block in contentBlocks)
        {
            var blockType = block?["type"]?.GetValue<string>();
            if (blockType == "text")
                text = block?["text"]?.GetValue<string>();
            else if (blockType == "tool_use")
            {
                var toolName = block?["name"]?.GetValue<string>() ?? string.Empty;
                var toolId = block?["id"]?.GetValue<string>() ?? string.Empty;
                var inputJson = block?["input"]?.ToJsonString() ?? "{}";
                toolCalls.Add(new LlmToolCall(toolName, inputJson, toolId));
            }
        }

        logger.LogDebug("Anthropic resposta: in={In} out={Out} stopReason={Stop}", usage.InputTokens, usage.OutputTokens, stopReason);

        return new LlmCompletion(text, toolCalls, usage, stopReason);
    }

    public async Task<string?> AnalisarImagemAsync(
        string systemPrompt,
        string userText,
        string imagemBase64,
        string mimeType,
        CancellationToken cancellationToken)
    {
        var opts = options.Value;

        var body = new Dictionary<string, object>
        {
            ["model"] = opts.ReasoningModel,
            ["max_tokens"] = 1024,
            ["system"] = systemPrompt,
            ["messages"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["role"] = "user",
                    ["content"] = new object[]
                    {
                        new
                        {
                            type = "image",
                            source = new
                            {
                                type = "base64",
                                media_type = mimeType,
                                data = imagemBase64
                            }
                        },
                        new { type = "text", text = userText }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(body, JsonOpts);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        logger.LogDebug("Chamando Anthropic Vision API modelo={Model}", opts.ReasoningModel);

        using var response = await httpClient.PostAsync("messages", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Anthropic Vision erro {Status}: {Body}", (int)response.StatusCode, error);
            return null;
        }

        var node = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
        return node?["content"]?.AsArray()
            .FirstOrDefault(b => b?["type"]?.GetValue<string>() == "text")
            ?["text"]?.GetValue<string>();
    }
}
