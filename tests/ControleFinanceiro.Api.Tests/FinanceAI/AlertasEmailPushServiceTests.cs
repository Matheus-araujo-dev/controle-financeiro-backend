using System.Net.Http.Json;
using ControleFinanceiro.Api.Configuration;
using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Common.Alertas;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.FinanceAI;
using ControleFinanceiro.Domain.FinanceAI;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.Identidade;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Api.Tests.FinanceAI;

public sealed class AlertasEmailPushServiceTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    // ── Fakes ────────────────────────────────────────────────────────────────

    private sealed class FakeEmail : IEmailAlertaService
    {
        public List<(string Destinatario, string Assunto, string HtmlBody)> Enviados { get; } = [];

        public Task<bool> EnviarAsync(string destinatario, string assunto, string htmlBody, CancellationToken ct)
        {
            Enviados.Add((destinatario, assunto, htmlBody));
            return Task.FromResult(true);
        }
    }

    private sealed class FakePush : IPushAlertaService
    {
        public List<(string Titulo, string Corpo)> Enviados { get; } = [];

        public string GetVapidPublicKey() => "fake-vapid-key";

        public Task<bool> EnviarAsync(PushSubscription subscription, string titulo, string corpo, CancellationToken ct)
        {
            Enviados.Add((titulo, corpo));
            return Task.FromResult(true);
        }
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private AlertasEmailPushService CriarServico(IAppDbContext db, FakeEmail email, FakePush push) =>
        new(db, email, push, NullLogger<AlertasEmailPushService>.Instance);

    // ── Testes ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessarAsync_SemConfiguracoes_NaoEnviaEmailNemPush()
    {
        await _factory.ResetDatabaseAsync();

        using var scope = _factory.Services.CreateWorkspaceScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var email = new FakeEmail();
        var push = new FakePush();

        var servico = CriarServico(db, email, push);
        await servico.ProcessarAsync(CancellationToken.None);

        email.Enviados.Should().BeEmpty();
        push.Enviados.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessarAsync_ContaPendenteNoVencimentoEmailAtivo_EnviaEmail()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var familiaId = _factory.Services
            .GetRequiredService<IOptions<AuthOptions>>().Value.DevelopmentFamiliaId;

        const int diasAntecedencia = 3;
        var vencimento = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(diasAntecedencia);

        // Conta a pagar pendente com vencimento no dia-alvo
        var contaResp = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            recebedorId = fixture.RecebedorId,
            dataVencimento = vencimento.ToString("yyyy-MM-dd"),
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 300m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta Email Test",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 300m } }
        });
        contaResp.EnsureSuccessStatusCode();

        // Usuario + configuracao de e-mail
        using var scope = _factory.Services.CreateWorkspaceScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var usuario = Usuario.Criar("sub-email-push-test", "emailpush@test.local", "Usuario Email Push", null);
        db.Usuarios.Add(usuario);

        var cfg = ConfiguracaoNotificacao.CriarPadrao(familiaId, usuario.Id);
        cfg.Atualizar(
            emailAtivo: true,
            emailDestinatario: "emailpush@test.local",
            emailVencimento: true,
            emailDiasAntecedencia: diasAntecedencia,
            emailLimiteCategoria: false,
            pushAtivo: false,
            pushVencimento: false,
            pushDiasAntecedencia: 1,
            pushLimiteCategoria: false);
        db.ConfiguracoesNotificacao.Add(cfg);
        await db.SaveChangesAsync(CancellationToken.None);

        var email = new FakeEmail();
        var push = new FakePush();
        var servico = CriarServico(db, email, push);
        await servico.ProcessarAsync(CancellationToken.None);

        email.Enviados.Should().ContainSingle();
        email.Enviados[0].Destinatario.Should().Be("emailpush@test.local");
        email.Enviados[0].HtmlBody.Should().Contain("Conta Email Test");
        push.Enviados.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessarAsync_ContaPendentePushAtivo_EnviaPush()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var familiaId = _factory.Services
            .GetRequiredService<IOptions<AuthOptions>>().Value.DevelopmentFamiliaId;

        const int diasAntecedencia = 1;
        var vencimento = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(diasAntecedencia);

        // Conta a pagar pendente com vencimento amanha
        var contaResp = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            recebedorId = fixture.RecebedorId,
            dataVencimento = vencimento.ToString("yyyy-MM-dd"),
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 150m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta Push Test",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 150m } }
        });
        contaResp.EnsureSuccessStatusCode();

        // Usuario + configuracao de push + subscription
        using var scope = _factory.Services.CreateWorkspaceScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var usuario = Usuario.Criar("sub-push-test", "push@test.local", "Usuario Push", null);
        db.Usuarios.Add(usuario);

        var cfg = ConfiguracaoNotificacao.CriarPadrao(familiaId, usuario.Id);
        cfg.Atualizar(
            emailAtivo: false,
            emailDestinatario: null,
            emailVencimento: false,
            emailDiasAntecedencia: 3,
            emailLimiteCategoria: false,
            pushAtivo: true,
            pushVencimento: true,
            pushDiasAntecedencia: diasAntecedencia,
            pushLimiteCategoria: false);
        db.ConfiguracoesNotificacao.Add(cfg);

        var sub = PushSubscription.Registrar(familiaId, usuario.Id, "https://push.example.com/endpoint", "fakep256dh", "fakeauth");
        db.PushSubscriptions.Add(sub);

        await db.SaveChangesAsync(CancellationToken.None);

        var email = new FakeEmail();
        var push = new FakePush();
        var servico = CriarServico(db, email, push);
        await servico.ProcessarAsync(CancellationToken.None);

        push.Enviados.Should().ContainSingle();
        push.Enviados[0].Titulo.Should().Contain(vencimento.ToString("dd/MM"));
        email.Enviados.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessarAsync_EmailJaEnviadoHoje_NaoEnviaNovamente()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var familiaId = _factory.Services
            .GetRequiredService<IOptions<AuthOptions>>().Value.DevelopmentFamiliaId;

        const int diasAntecedencia = 2;
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var vencimento = hoje.AddDays(diasAntecedencia);

        // Conta pendente no dia-alvo
        var contaResp = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = hoje.ToString("yyyy-MM-dd"),
            recebedorId = fixture.RecebedorId,
            dataVencimento = vencimento.ToString("yyyy-MM-dd"),
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 80m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta Dedup Test",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 80m } }
        });
        contaResp.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateWorkspaceScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var usuario = Usuario.Criar("sub-dedup-test", "dedup@test.local", "Usuario Dedup", null);
        db.Usuarios.Add(usuario);

        var cfg = ConfiguracaoNotificacao.CriarPadrao(familiaId, usuario.Id);
        cfg.Atualizar(
            emailAtivo: true,
            emailDestinatario: "dedup@test.local",
            emailVencimento: true,
            emailDiasAntecedencia: diasAntecedencia,
            emailLimiteCategoria: false,
            pushAtivo: false,
            pushVencimento: false,
            pushDiasAntecedencia: 1,
            pushLimiteCategoria: false);
        db.ConfiguracoesNotificacao.Add(cfg);

        // Pre-registrar o envio de hoje para simular deduplicacao
        var chave = vencimento.ToString("yyyyMMdd");
        db.AlertasDigitaisEnviados.Add(
            AlertaDigitalEnviado.Registrar(usuario.Id, AlertaDigitalEnviado.CanalEmail, AlertaDigitalEnviado.TipoVencimento, chave, hoje));

        await db.SaveChangesAsync(CancellationToken.None);

        var email = new FakeEmail();
        var push = new FakePush();
        var servico = CriarServico(db, email, push);
        await servico.ProcessarAsync(CancellationToken.None);

        // Nao deve enviar de novo, pois ja ha registro de envio para hoje
        email.Enviados.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessarAsync_SemContaPendenteNoDiaAlvo_NaoEnviaEmail()
    {
        await _factory.ResetDatabaseAsync();

        var familiaId = _factory.Services
            .GetRequiredService<IOptions<AuthOptions>>().Value.DevelopmentFamiliaId;

        // Configuracao de email ativa, mas sem contas no dia-alvo
        using var scope = _factory.Services.CreateWorkspaceScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var usuario = Usuario.Criar("sub-no-conta-test", "noconta@test.local", "Sem Conta", null);
        db.Usuarios.Add(usuario);

        var cfg = ConfiguracaoNotificacao.CriarPadrao(familiaId, usuario.Id);
        cfg.Atualizar(
            emailAtivo: true,
            emailDestinatario: "noconta@test.local",
            emailVencimento: true,
            emailDiasAntecedencia: 5,
            emailLimiteCategoria: false,
            pushAtivo: false,
            pushVencimento: false,
            pushDiasAntecedencia: 1,
            pushLimiteCategoria: false);
        db.ConfiguracoesNotificacao.Add(cfg);
        await db.SaveChangesAsync(CancellationToken.None);

        var email = new FakeEmail();
        var push = new FakePush();
        var servico = CriarServico(db, email, push);
        await servico.ProcessarAsync(CancellationToken.None);

        email.Enviados.Should().BeEmpty();
        push.Enviados.Should().BeEmpty();
    }
}