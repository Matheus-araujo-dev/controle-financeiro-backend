using System.Net.Http.Json;
using ControleFinanceiro.Api.Configuration;
using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.FinanceAI;
using ControleFinanceiro.Domain.FinanceAI;
using ControleFinanceiro.Domain.Identidade;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Api.Tests.FinanceAI;

public sealed class AlertasVencimentoServiceTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private sealed class FakeOutbound : IWhatsappOutboundService
    {
        public List<(string Telefone, string Texto)> Enviados { get; } = [];

        public Task EnviarAsync(string telefone, string texto, CancellationToken cancellationToken)
        {
            Enviados.Add((telefone, texto));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ProcessarAsync_SemAlertasConfigurados_DeveCompletarSemEnviar()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        using var scope = _factory.Services.CreateScope();
        var outbound = new FakeOutbound();
        var servico = new AlertasVencimentoService(
            scope.ServiceProvider.GetRequiredService<IAppDbContext>(),
            outbound,
            NullLogger<AlertasVencimentoService>.Instance);

        await servico.ProcessarAsync(CancellationToken.None);

        outbound.Enviados.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessarAsync_ComContaVencendoEUsuarioComAlerta_DeveEnviarLembrete()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var familiaId = _factory.Services.GetRequiredService<IOptions<AuthOptions>>().Value.DevelopmentFamiliaId;
        var vencimento = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);

        // Conta a pagar pendente vencendo em 3 dias (criada via API → estampada na família dev)
        await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            recebedorId = fixture.RecebedorId,
            dataVencimento = vencimento.ToString("yyyy-MM-dd"),
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 250m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta a vencer",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 250m } }
        });

        // Usuário WhatsApp + config de alerta de vencimento (3 dias)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var usuario = Usuario.Criar("sub-alerta", "alerta@test.local", "Usuário Alerta", null);
        db.Usuarios.Add(usuario);
        var wup = WhatsappUsuario.Criar(familiaId, usuario.Id, "5531988887777");
        wup.Verificar(DateTimeOffset.UtcNow);
        db.WhatsappUsuarios.Add(wup);
        var cfg = WhatsappConfigAlerta.CriarPadrao(familiaId, usuario.Id);
        cfg.Atualizar(true, 3, false, false);
        db.WhatsappConfigAlertas.Add(cfg);
        await db.SaveChangesAsync(CancellationToken.None);

        var outbound = new FakeOutbound();
        var servico = new AlertasVencimentoService(db, outbound, NullLogger<AlertasVencimentoService>.Instance);

        await servico.ProcessarAsync(CancellationToken.None);

        outbound.Enviados.Should().ContainSingle();
        outbound.Enviados[0].Telefone.Should().Be("5531988887777");
        outbound.Enviados[0].Texto.Should().Contain("Conta a vencer");
    }
}
