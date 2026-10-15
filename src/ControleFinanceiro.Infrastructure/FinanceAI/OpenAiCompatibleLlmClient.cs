using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.FinanceAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Infrastructure.FinanceAI;

public sealed class OpenAiCompatibleLlmClient(
    HttpClient httpClient,
    IOptions<LlmOptions> options,
    ILogger<OpenAiCompatibleLlmClient> logger) : ILlmClient, ILlmVisionClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        var opts = options.Value;
        var model = request.Tier == LlmModelTier.Fast ? opts.FastModel : opts.ReasoningModel;

        var messages = new List<object>
        {
            new { role = "system", content = request.SystemPrompt }
        };

        foreach (var m in request.Messages)
        {
            var role = m.Role switch
            {
                LlmRole.Assistant => "assistant",
                LlmRole.ToolResult => "tool",
                _ => "user"
            };
            messages.Add(new { role, content = m.Content });
        }

        var body = new Dictionary<string, object>
        {
            ["model"] = model,
            ["max_tokens"] = opts.MaxTokens,
            ["messages"] = messages
        };

        if (request.Tools?.Count > 0)
        {
            body["tools"] = request.Tools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = JsonDocument.Parse(t.InputSchema).RootElement
                }
            }).ToList();
        }

        var json = JsonSerializer.Serialize(body, JsonOpts);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        logger.LogDebug("Chamando LLM API (OpenAI-compatible) modelo={Model} tier={Tier}", model, request.Tier);

        using var response = await httpClient.PostAsync("chat/completions", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("LLM API erro {Status}: {Body}", (int)response.StatusCode, error);
            throw new InvalidOperationException($"LLM API retornou {(int)response.StatusCode}: {error}");
        }

        var node = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("LLM API retornou resposta vazia.");

        var usage = new LlmUsage(
            node["usage"]?["prompt_tokens"]?.GetValue<int>() ?? 0,
            node["usage"]?["completion_tokens"]?.GetValue<int>() ?? 0);

        var choice = node["choices"]?.AsArray().FirstOrDefault();
        var finishReason = choice?["finish_reason"]?.GetValue<string>();
        var message = choice?["message"];

        var text = message?["content"]?.GetValue<string>();
        var toolCalls = new List<LlmToolCall>();

        var toolCallsArray = message?["tool_calls"]?.AsArray();
        if (toolCallsArray is not null)
        {
            foreach (var tc in toolCallsArray)
            {
                var fn = tc?["function"];
                var toolName = fn?["name"]?.GetValue<string>() ?? string.Empty;
                var toolId = tc?["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString();
                var inputJson = fn?["arguments"]?.GetValue<string>() ?? "{}";
                toolCalls.Add(new LlmToolCall(toolName, inputJson, toolId));
            }
        }

        var stopReason = finishReason switch
        {
            "tool_calls" => "tool_use",
            "stop" => "end_turn",
            _ => finishReason
        };

        logger.LogDebug("LLM resposta: in={In} out={Out} finishReason={Stop}", usage.InputTokens, usage.OutputTokens, stopReason);

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
            ["messages"] = new object[]
            {
                new { role = "system", content = systemPrompt },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "image_url",
                            image_url = new { url = $"data:{mimeType};base64,{imagemBase64}" }
                        },
                        new { type = "text", text = userText }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(body, JsonOpts);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        logger.LogDebug("Chamando LLM Vision API (OpenAI-compatible) modelo={Model}", opts.ReasoningModel);

        using var response = await httpClient.PostAsync("chat/completions", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("LLM Vision erro {Status}: {Body}", (int)response.StatusCode, error);
            return null;
        }

        var node = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
        return node?["choices"]?.AsArray().FirstOrDefault()
            ?["message"]?["content"]?.GetValue<string>();
    }
}
