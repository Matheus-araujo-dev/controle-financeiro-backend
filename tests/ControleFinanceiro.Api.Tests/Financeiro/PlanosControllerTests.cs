using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Contracts.Financeiro.Planos;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class PlanosControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static async Task<Guid> CriarContaBancariaAsync(HttpClient client, string nome = "Caixa Plano")
    {
        var resp = await client.PostAsJsonAsync("/api/v1/contas-bancarias", new
        {
            nome,
            banco = "Banco Teste",
            saldoInicial = 0m,
            dataSaldoInicial = "2026-01-01"
        });
        resp.EnsureSuccessStatusCode();
        var obj = await resp.Content.ReadFromJsonAsync<ContaBancariaResumoResponse>();
        return obj!.Id;
    }

    [Fact]
    public async Task Post_DeveCriarPlanoERetornarCreated()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var contaId = await CriarContaBancariaAsync(client);

        var resp = await client.PostAsJsonAsync("/api/v1/planos", new
        {
            nome = "Viagem Europa",
            descricao = "Reserva mensal para viagem",
            valorMensal = 500m,
            numParcelas = 24,
            contaBancariaCaixaId = contaId
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var plano = await resp.Content.ReadFromJsonAsync<PlanoResumoResponse>();
        plano.Should().NotBeNull();
        plano!.Nome.Should().Be("Viagem Europa");
        plano.ValorMensal.Should().Be(500m);
        plano.NumParcelas.Should().Be(24);
        plano.ParcelasPagas.Should().Be(0);
        plano.Concluido.Should().BeFalse();
        plano.Cancelado.Should().BeFalse();
    }

    [Fact]
    public async Task Get_DeveListarPlanosDoTenant()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var contaId = await CriarContaBancariaAsync(client);
        await client.PostAsJsonAsync("/api/v1/planos", new
        {
            nome = "Fundo Emergência",
            valorMensal = 300m,
            numParcelas = 12,
            contaBancariaCaixaId = contaId
        });

        var lista = await client.GetFromJsonAsync<PlanoListResponse>("/api/v1/planos");

        lista.Should().NotBeNull();
        lista!.Items.Should().HaveCount(1);
        lista.Items[0].Nome.Should().Be("Fundo Emergência");
    }

    [Fact]
    public async Task GetById_DeveRetornarPlanoCriado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var contaId = await CriarContaBancariaAsync(client);
        var createResp = await client.PostAsJsonAsync("/api/v1/planos", new
        {
            nome = "Reserva Carro",
            valorMensal = 800m,
            numParcelas = 36,
            contaBancariaCaixaId = contaId
        });
        var criado = await createResp.Content.ReadFromJsonAsync<PlanoResumoResponse>();

        var detail = await client.GetFromJsonAsync<PlanoResumoResponse>($"/api/v1/planos/{criado!.Id}");

        detail.Should().NotBeNull();
        detail!.Nome.Should().Be("Reserva Carro");
    }

    [Fact]
    public async Task GetById_QuandoNaoEncontrado_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.GetAsync($"/api/v1/planos/{Guid.NewGuid()}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_DeveAtualizarNomeEValorMensal()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var contaId = await CriarContaBancariaAsync(client);
        var createResp = await client.PostAsJsonAsync("/api/v1/planos", new
        {
            nome = "Plano Original",
            valorMensal = 200m,
            numParcelas = 6,
            contaBancariaCaixaId = contaId
        });
        var criado = await createResp.Content.ReadFromJsonAsync<PlanoResumoResponse>();

        var updateResp = await client.PutAsJsonAsync($"/api/v1/planos/{criado!.Id}", new
        {
            nome = "Plano Atualizado",
            valorMensal = 400m,
            numParcelas = 6
        });

        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var atualizado = await updateResp.Content.ReadFromJsonAsync<PlanoResumoResponse>();
        atualizado!.Nome.Should().Be("Plano Atualizado");
        atualizado.ValorMensal.Should().Be(400m);
    }

    [Fact]
    public async Task Post_AdiantarParcela_DeveIncrementarParcelasPagas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var contaId = await CriarContaBancariaAsync(client);
        var createResp = await client.PostAsJsonAsync("/api/v1/planos", new
        {
            nome = "Plano Adiantar",
            valorMensal = 100m,
            numParcelas = 5,
            contaBancariaCaixaId = contaId
        });
        var criado = await createResp.Content.ReadFromJsonAsync<PlanoResumoResponse>();

        var adiantarResp = await client.PostAsync($"/api/v1/planos/{criado!.Id}/adiantar-parcela", null);

        adiantarResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var plano = await adiantarResp.Content.ReadFromJsonAsync<PlanoResumoResponse>();
        plano!.ParcelasPagas.Should().Be(1);
    }

    [Fact]
    public async Task Post_RetirarDinheiro_DeveIncrementarTotalRetirado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var contaId = await CriarContaBancariaAsync(client);
        var createResp = await client.PostAsJsonAsync("/api/v1/planos", new
        {
            nome = "Plano Retirada",
            valorMensal = 500m,
            numParcelas = 12,
            contaBancariaCaixaId = contaId
        });
        var criado = await createResp.Content.ReadFromJsonAsync<PlanoResumoResponse>();

        // Acumular saldo
        await client.PostAsync($"/api/v1/planos/{criado!.Id}/adiantar-parcela", null);
        await client.PostAsync($"/api/v1/planos/{criado.Id}/adiantar-parcela", null);

        var retirarResp = await client.PostAsJsonAsync($"/api/v1/planos/{criado.Id}/retirar", new { valor = 200m });

        retirarResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var plano = await retirarResp.Content.ReadFromJsonAsync<PlanoResumoResponse>();
        plano!.TotalRetirado.Should().Be(200m);
    }

    [Fact]
    public async Task Delete_DeveCancelarPlano()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var contaId = await CriarContaBancariaAsync(client);
        var createResp = await client.PostAsJsonAsync("/api/v1/planos", new
        {
            nome = "Plano Cancelar",
            valorMensal = 150m,
            numParcelas = 3,
            contaBancariaCaixaId = contaId
        });
        var criado = await createResp.Content.ReadFromJsonAsync<PlanoResumoResponse>();

        var deleteResp = await client.DeleteAsync($"/api/v1/planos/{criado!.Id}");

        deleteResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var plano = await deleteResp.Content.ReadFromJsonAsync<PlanoResumoResponse>();
        plano!.Cancelado.Should().BeTrue();
    }

    [Fact]
    public async Task Post_QuandoDadosInvalidos_DeveRetornar400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/planos", new
        {
            nome = "",
            valorMensal = -100m,
            numParcelas = 0,
            contaBancariaCaixaId = Guid.NewGuid()
        });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

internal record ContaBancariaResumoResponse(Guid Id, string Nome);
