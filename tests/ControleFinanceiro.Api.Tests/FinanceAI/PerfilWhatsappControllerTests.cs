using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Contracts.Agente;
using ControleFinanceiro.Domain.Identidade;
using ControleFinanceiro.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Api.Tests.FinanceAI;

public sealed class PerfilWhatsappControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    // O dev-auth usa o header X-Debug-User como userId quando ele é um Guid; semeando um Usuario real
    // com esse Id, as FKs de WhatsappUsuario/WhatsappConfigAlerta (→ Usuario) ficam satisfeitas.
    private async Task<HttpClient> CriarClientComUsuarioSeedAsync()
    {
        Guid usuarioId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var usuario = Usuario.Criar("sub-perfil", "perfil@test.local", "Perfil Teste", null);
            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync();
            usuarioId = usuario.Id;
        }

        return _factory.CreateAuthenticatedClient(usuarioId.ToString());
    }

    [Fact]
    public async Task FluxoCompletoDePerfil_DeveRegistrarAtualizarEDesativar()
    {
        await _factory.ResetDatabaseAsync();
        using var client = await CriarClientComUsuarioSeedAsync();

        var inicial = await client.GetFromJsonAsync<WhatsappPerfilResponse>("/api/v1/perfil/whatsapp");
        inicial!.Telefone.Should().BeNull();
        inicial.Ativo.Should().BeFalse();

        var registrar = await client.PutAsJsonAsync("/api/v1/perfil/whatsapp",
            new WhatsappRegistrarRequest("+55 (31) 99999-8888"));
        registrar.StatusCode.Should().Be(HttpStatusCode.OK);
        var registrado = await registrar.Content.ReadFromJsonAsync<WhatsappPerfilResponse>();
        registrado!.Telefone.Should().Be("5531999998888");
        registrado.Ativo.Should().BeTrue();
        registrado.VerificadoEm.Should().NotBeNull();

        var apos = await client.GetFromJsonAsync<WhatsappPerfilResponse>("/api/v1/perfil/whatsapp");
        apos!.Telefone.Should().Be("5531999998888");

        var atualizar = await client.PutAsJsonAsync("/api/v1/perfil/whatsapp",
            new WhatsappRegistrarRequest("5531777776666"));
        var atualizado = await atualizar.Content.ReadFromJsonAsync<WhatsappPerfilResponse>();
        atualizado!.Telefone.Should().Be("5531777776666");

        var desativar = await client.DeleteAsync("/api/v1/perfil/whatsapp");
        desativar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var final = await client.GetFromJsonAsync<WhatsappPerfilResponse>("/api/v1/perfil/whatsapp");
        final!.Ativo.Should().BeFalse();
    }

    [Fact]
    public async Task Registrar_ComTelefoneInvalido_DeveRetornar400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = await CriarClientComUsuarioSeedAsync();

        var resposta = await client.PutAsJsonAsync("/api/v1/perfil/whatsapp",
            new WhatsappRegistrarRequest("123"));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Alertas_DeveRetornarPadraoEPermitirAtualizar()
    {
        await _factory.ResetDatabaseAsync();
        using var client = await CriarClientComUsuarioSeedAsync();

        var padrao = await client.GetFromJsonAsync<WhatsappAlertasResponse>("/api/v1/perfil/whatsapp/alertas");
        padrao!.ReceberVencimento.Should().BeTrue();
        padrao.DiasAntecedenciaVencimento.Should().Be(3);
        padrao.ReceberLimiteCategoria.Should().BeFalse();

        var atualizar = await client.PutAsJsonAsync("/api/v1/perfil/whatsapp/alertas",
            new WhatsappAlertasRequest(false, 7, true, true));
        atualizar.StatusCode.Should().Be(HttpStatusCode.OK);
        var atualizado = await atualizar.Content.ReadFromJsonAsync<WhatsappAlertasResponse>();
        atualizado!.ReceberVencimento.Should().BeFalse();
        atualizado.DiasAntecedenciaVencimento.Should().Be(7);
        atualizado.ReceberLimiteCategoria.Should().BeTrue();
        atualizado.ReceberLimiteResponsavel.Should().BeTrue();

        var relido = await client.GetFromJsonAsync<WhatsappAlertasResponse>("/api/v1/perfil/whatsapp/alertas");
        relido!.DiasAntecedenciaVencimento.Should().Be(7);
    }

    [Fact]
    public async Task Endpoints_SemAutenticacao_DeveRetornar401()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAnonymousClient();

        var resposta = await client.GetAsync("/api/v1/perfil/whatsapp");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
