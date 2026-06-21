using System.Net;
using System.Text;
using System.Text.Json;
using ControleFinanceiro.Application.FinanceAI;
using ControleFinanceiro.Infrastructure.FinanceAI;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Infrastructure.Tests.FinanceAI;

public sealed class AnthropicLlmClientTests
{
    private sealed class StubHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public string? CapturedBody { get; private set; }
        public Uri? CapturedUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedUri = request.RequestUri;
            CapturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private static (AnthropicLlmClient client, StubHandler handler) CriarClient(
        HttpStatusCode status,
        string responseBody,
        LlmOptions? options = null)
    {
        var handler = new StubHandler(status, responseBody);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        options ??= new LlmOptions { FastModel = "fast-x", ReasoningModel = "reason-y", MaxTokens = 555 };
        var client = new AnthropicLlmClient(
            httpClient,
            Options.Create(options),
            NullLogger<AnthropicLlmClient>.Instance);
        return (client, handler);
    }

    private static LlmRequest PerguntaSimples(LlmModelTier tier = LlmModelTier.Fast) =>
        new(tier, "system", [new LlmMessage(LlmRole.User, "olá")]);

    [Fact]
    public async Task CompleteAsync_DeveParsearTextoUsoEStopReason()
    {
        const string resposta = """
            {"content":[{"type":"text","text":"Resposta do modelo"}],
             "stop_reason":"end_turn","usage":{"input_tokens":12,"output_tokens":34}}
            """;
        var (client, _) = CriarClient(HttpStatusCode.OK, resposta);

        var completion = await client.CompleteAsync(PerguntaSimples(), CancellationToken.None);

        completion.Text.Should().Be("Resposta do modelo");
        completion.StopReason.Should().Be("end_turn");
        completion.Usage.InputTokens.Should().Be(12);
        completion.Usage.OutputTokens.Should().Be(34);
        completion.ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task CompleteAsync_DeveExtrairToolUse()
    {
        const string resposta = """
            {"content":[
                {"type":"text","text":"vou usar uma ferramenta"},
                {"type":"tool_use","id":"tool-1","name":"buscar_saldo","input":{"conta":"x"}}
             ],
             "stop_reason":"tool_use","usage":{"input_tokens":1,"output_tokens":2}}
            """;
        var (client, _) = CriarClient(HttpStatusCode.OK, resposta);

        var completion = await client.CompleteAsync(PerguntaSimples(), CancellationToken.None);

        completion.ToolCalls.Should().ContainSingle();
        var tool = completion.ToolCalls[0];
        tool.ToolName.Should().Be("buscar_saldo");
        tool.ToolUseId.Should().Be("tool-1");
        tool.InputJson.Should().Contain("\"conta\"").And.Contain("x");
    }

    [Theory]
    [InlineData(LlmModelTier.Fast, "fast-x")]
    [InlineData(LlmModelTier.Reasoning, "reason-y")]
    public async Task CompleteAsync_DeveSelecionarModeloPorTier(LlmModelTier tier, string modeloEsperado)
    {
        const string resposta = """{"content":[{"type":"text","text":"ok"}],"usage":{"input_tokens":0,"output_tokens":0}}""";
        var (client, handler) = CriarClient(HttpStatusCode.OK, resposta);

        await client.CompleteAsync(PerguntaSimples(tier), CancellationToken.None);

        using var doc = JsonDocument.Parse(handler.CapturedBody!);
        doc.RootElement.GetProperty("model").GetString().Should().Be(modeloEsperado);
        doc.RootElement.GetProperty("max_tokens").GetInt32().Should().Be(555);
    }

    [Fact]
    public async Task CompleteAsync_ComTools_DeveIncluirSchemaNoCorpo()
    {
        const string resposta = """{"content":[{"type":"text","text":"ok"}],"usage":{"input_tokens":0,"output_tokens":0}}""";
        var (client, handler) = CriarClient(HttpStatusCode.OK, resposta);
        var request = new LlmRequest(
            LlmModelTier.Fast,
            "system",
            [new LlmMessage(LlmRole.User, "oi")],
            [new LlmToolDefinition("minha_tool", "faz algo", "{\"type\":\"object\"}")]);

        await client.CompleteAsync(request, CancellationToken.None);

        using var doc = JsonDocument.Parse(handler.CapturedBody!);
        var tools = doc.RootElement.GetProperty("tools");
        tools.GetArrayLength().Should().Be(1);
        tools[0].GetProperty("name").GetString().Should().Be("minha_tool");
    }

    [Fact]
    public async Task CompleteAsync_ComErroHttp_DeveLancar()
    {
        var (client, _) = CriarClient(HttpStatusCode.InternalServerError, "boom");

        var acao = async () => await client.CompleteAsync(PerguntaSimples(), CancellationToken.None);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AnalisarImagemAsync_ComSucesso_DeveRetornarTexto()
    {
        const string resposta = """{"content":[{"type":"text","text":"recibo de mercado"}]}""";
        var (client, handler) = CriarClient(HttpStatusCode.OK, resposta);

        var texto = await ((ILlmVisionClient)client).AnalisarImagemAsync(
            "system", "extraia", "BASE64", "image/png", CancellationToken.None);

        texto.Should().Be("recibo de mercado");
        handler.CapturedBody.Should().Contain("base64").And.Contain("image/png");
    }

    [Fact]
    public async Task AnalisarImagemAsync_ComErro_DeveRetornarNull()
    {
        var (client, _) = CriarClient(HttpStatusCode.BadRequest, "erro");

        var texto = await ((ILlmVisionClient)client).AnalisarImagemAsync(
            "system", "extraia", "BASE64", "image/png", CancellationToken.None);

        texto.Should().BeNull();
    }
}
