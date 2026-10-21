using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Contracts.Alertas;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class AlertasControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Get_Configuracao_QuandoNaoExiste_DeveRetornarPadrao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/v1/alertas/configuracao");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var cfg = await resp.Content.ReadFromJsonAsync<ConfiguracaoNotificacaoResponse>();
        cfg.Should().NotBeNull();
        cfg!.EmailAtivo.Should().BeFalse();
        cfg.PushAtivo.Should().BeFalse();
    }

    [Fact]
    public async Task Put_Configuracao_DeveSalvarERetornarConfiguracaoAtualizada()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var putResp = await client.PutAsJsonAsync("/api/v1/alertas/configuracao", new
        {
            emailAtivo = true,
            emailDestinatario = "teste@exemplo.com",
            emailVencimento = true,
            emailDiasAntecedencia = 5,
            emailLimiteCategoria = false,
            pushAtivo = false,
            pushVencimento = true,
            pushDiasAntecedencia = 3,
            pushLimiteCategoria = false
        });

        putResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var cfg = await putResp.Content.ReadFromJsonAsync<ConfiguracaoNotificacaoResponse>();
        cfg!.EmailAtivo.Should().BeTrue();
        cfg.EmailDestinatario.Should().Be("teste@exemplo.com");
        cfg.EmailDiasAntecedencia.Should().Be(5);
    }

    [Fact]
    public async Task Put_ConfiguracaoEGet_DeveRetornarValoresSalvos()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await client.PutAsJsonAsync("/api/v1/alertas/configuracao", new
        {
            emailAtivo = true,
            emailDestinatario = "usuario@teste.com",
            emailVencimento = false,
            emailDiasAntecedencia = 7,
            emailLimiteCategoria = true,
            pushAtivo = true,
            pushVencimento = false,
            pushDiasAntecedencia = 1,
            pushLimiteCategoria = false
        });

        var getResp = await client.GetAsync("/api/v1/alertas/configuracao");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var cfg = await getResp.Content.ReadFromJsonAsync<ConfiguracaoNotificacaoResponse>();
        cfg!.EmailAtivo.Should().BeTrue();
        cfg.EmailDestinatario.Should().Be("usuario@teste.com");
        cfg.PushAtivo.Should().BeTrue();
    }

    [Fact]
    public async Task Get_VapidKey_DeveRetornarChavePublica()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/v1/alertas/push/vapid-key");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var vapid = await resp.Content.ReadFromJsonAsync<VapidPublicKeyResponse>();
        vapid.Should().NotBeNull();
        // PublicKey pode ser vazio em ambiente de teste sem VAPID configurado
    }

    [Fact]
    public async Task Get_Subscriptions_QuandoNenhuma_DeveRetornarListaVazia()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/v1/alertas/push/subscriptions");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var subs = await resp.Content.ReadFromJsonAsync<List<PushSubscriptionResponse>>();
        subs.Should().NotBeNull();
        subs.Should().BeEmpty();
    }

    [Fact]
    public async Task Post_RegistrarSubscription_DeveRetornarCreated()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/alertas/push/subscriptions", new
        {
            endpoint = "https://fcm.googleapis.com/fcm/send/teste-endpoint-unico-123",
            p256dh = "BNcRdreALRFXTkOOUHK1EtK2wtZ5MZwwHQLHOfz9In4wBLCBPpoWm1E5EFw1JXa1w==",
            auth = "tBHItJI5svbpez7KI4CCXg=="
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var sub = await resp.Content.ReadFromJsonAsync<PushSubscriptionResponse>();
        sub.Should().NotBeNull();
        sub!.Ativo.Should().BeTrue();
        sub.Endpoint.Should().Be("https://fcm.googleapis.com/fcm/send/teste-endpoint-unico-123");
    }

    [Fact]
    public async Task Post_RegistrarSubscriptionDuplicada_DeveRetornarExistente()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var payload = new
        {
            endpoint = "https://fcm.googleapis.com/fcm/send/endpoint-duplicado",
            p256dh = "BNcRdreALRFXTkOOUHK1EtK2wtZ5MZwwHQLHOfz9In4wBLCBPpoWm1E5EFw1JXa1w==",
            auth = "tBHItJI5svbpez7KI4CCXg=="
        };

        var resp1 = await client.PostAsJsonAsync("/api/v1/alertas/push/subscriptions", payload);
        var resp2 = await client.PostAsJsonAsync("/api/v1/alertas/push/subscriptions", payload);

        resp1.IsSuccessStatusCode.Should().BeTrue();
        resp2.IsSuccessStatusCode.Should().BeTrue();

        // Deve retornar a mesma subscription (idempotência)
        var sub1 = await resp1.Content.ReadFromJsonAsync<PushSubscriptionResponse>();
        var sub2 = await resp2.Content.ReadFromJsonAsync<PushSubscriptionResponse>();
        sub1!.Id.Should().Be(sub2!.Id);
    }

    [Fact]
    public async Task Delete_Subscription_DeveRemoverERetornar204()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var createResp = await client.PostAsJsonAsync("/api/v1/alertas/push/subscriptions", new
        {
            endpoint = "https://fcm.googleapis.com/fcm/send/endpoint-para-deletar",
            p256dh = "BNcRdreALRFXTkOOUHK1EtK2wtZ5MZwwHQLHOfz9In4wBLCBPpoWm1E5EFw1JXa1w==",
            auth = "tBHItJI5svbpez7KI4CCXg=="
        });
        var sub = await createResp.Content.ReadFromJsonAsync<PushSubscriptionResponse>();

        var deleteResp = await client.DeleteAsync($"/api/v1/alertas/push/subscriptions/{sub!.Id}");

        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Subscription deve sumir da lista
        var subs = await client.GetFromJsonAsync<List<PushSubscriptionResponse>>("/api/v1/alertas/push/subscriptions");
        subs.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_SubscriptionInexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.DeleteAsync($"/api/v1/alertas/push/subscriptions/{Guid.NewGuid()}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
