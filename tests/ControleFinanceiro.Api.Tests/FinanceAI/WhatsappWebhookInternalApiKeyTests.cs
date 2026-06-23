using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Contracts.Agente;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ControleFinanceiro.Api.Tests.FinanceAI;

public sealed class WhatsappWebhookInternalApiKeyTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static WhatsappMensagemInboundRequest Mensagem() => new(
        Telefone: "5531900000000",
        Tipo: "texto",
        Texto: "olá",
        MidiaBase64: null,
        MimeType: null,
        MessageId: "msg-1",
        NomeArquivo: null,
        Timestamp: DateTimeOffset.UtcNow);

    [Fact]
    public async Task Webhook_SemApiKeyConfigurada_DeveRetornar503()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resposta = await client.PostAsJsonAsync("/api/v1/agente/whatsapp/mensagem", Mensagem());

        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Webhook_ComApiKeyConfigurada_DeveExigirHeaderCorreto()
    {
        using var configurada = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("WhatsappBridge:ApiKey", "segredo-interno"));

        // Sem header → 401
        using var semHeader = configurada.CreateClient();
        var r401 = await semHeader.PostAsJsonAsync("/api/v1/agente/whatsapp/mensagem", Mensagem());
        r401.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Com header correto → 200 (telefone não vinculado → resposta tratada pelo serviço)
        using var comHeader = configurada.CreateClient();
        comHeader.DefaultRequestHeaders.Add("X-Internal-ApiKey", "segredo-interno");
        var r200 = await comHeader.PostAsJsonAsync("/api/v1/agente/whatsapp/mensagem", Mensagem());
        r200.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await r200.Content.ReadFromJsonAsync<WhatsappMensagemInboundResponse>();
        corpo.Should().NotBeNull();
    }
}
