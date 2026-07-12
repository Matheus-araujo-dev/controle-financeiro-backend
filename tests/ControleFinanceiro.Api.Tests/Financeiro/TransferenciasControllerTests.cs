using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Contracts.Financeiro.Transferencias;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class TransferenciasControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static async Task<Guid> CriarContaBancariaAsync(HttpClient client, string nome)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/contas-bancarias", new
        {
            nome,
            banco = "Banco Teste",
            saldoInicial = 5000m,
            dataSaldoInicial = "2026-01-01"
        });
        resp.EnsureSuccessStatusCode();
        var obj = await resp.Content.ReadFromJsonAsync<ContaBancariaTransfResponse>();
        return obj!.Id;
    }

    [Fact]
    public async Task Post_DeveCriarTransferenciaERetornarCreated()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var origemId = await CriarContaBancariaAsync(client, "Conta Origem");
        var destinoId = await CriarContaBancariaAsync(client, "Conta Destino");

        var resp = await client.PostAsJsonAsync("/api/v1/transferencias", new
        {
            contaBancariaOrigemId = origemId,
            contaBancariaDestinoId = destinoId,
            valor = 1000m,
            dataTransferencia = "2026-06-15",
            descricao = "Transferência de teste"
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var tf = await resp.Content.ReadFromJsonAsync<TransferenciaResumoResponse>();
        tf.Should().NotBeNull();
        tf!.Valor.Should().Be(1000m);
        tf.Cancelada.Should().BeFalse();
        tf.OrigemNome.Should().NotBeNullOrEmpty();
        tf.DestinoNome.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Get_DeveListarTransferencias()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var origemId = await CriarContaBancariaAsync(client, "Conta A");
        var destinoId = await CriarContaBancariaAsync(client, "Conta B");

        await client.PostAsJsonAsync("/api/v1/transferencias", new
        {
            contaBancariaOrigemId = origemId,
            contaBancariaDestinoId = destinoId,
            valor = 500m,
            dataTransferencia = "2026-06-20"
        });

        var lista = await client.GetFromJsonAsync<TransferenciaListResponse>("/api/v1/transferencias");

        lista.Should().NotBeNull();
        lista!.Items.Should().HaveCount(1);
        lista.Items[0].Valor.Should().Be(500m);
    }

    [Fact]
    public async Task GetById_DeveRetornarTransferenciaCriada()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var origemId = await CriarContaBancariaAsync(client, "Conta X");
        var destinoId = await CriarContaBancariaAsync(client, "Conta Y");

        var createResp = await client.PostAsJsonAsync("/api/v1/transferencias", new
        {
            contaBancariaOrigemId = origemId,
            contaBancariaDestinoId = destinoId,
            valor = 250m,
            dataTransferencia = "2026-07-01"
        });
        var criada = await createResp.Content.ReadFromJsonAsync<TransferenciaResumoResponse>();

        var detail = await client.GetFromJsonAsync<TransferenciaResumoResponse>($"/api/v1/transferencias/{criada!.Id}");

        detail.Should().NotBeNull();
        detail!.Valor.Should().Be(250m);
        detail.Id.Should().Be(criada.Id);
    }

    [Fact]
    public async Task GetById_QuandoNaoEncontrado_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.GetAsync($"/api/v1/transferencias/{Guid.NewGuid()}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_DeveCancelarTransferencia()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var origemId = await CriarContaBancariaAsync(client, "Conta C");
        var destinoId = await CriarContaBancariaAsync(client, "Conta D");

        var createResp = await client.PostAsJsonAsync("/api/v1/transferencias", new
        {
            contaBancariaOrigemId = origemId,
            contaBancariaDestinoId = destinoId,
            valor = 100m,
            dataTransferencia = "2026-07-05"
        });
        var criada = await createResp.Content.ReadFromJsonAsync<TransferenciaResumoResponse>();

        var deleteResp = await client.DeleteAsync($"/api/v1/transferencias/{criada!.Id}");

        deleteResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelada = await deleteResp.Content.ReadFromJsonAsync<TransferenciaResumoResponse>();
        cancelada!.Cancelada.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_QuandoNaoEncontrada_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.DeleteAsync($"/api/v1/transferencias/{Guid.NewGuid()}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ComFiltroDeData_DeveRetornarSomenteNoPeriodo()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var origemId = await CriarContaBancariaAsync(client, "Conta E");
        var destinoId = await CriarContaBancariaAsync(client, "Conta F");

        await client.PostAsJsonAsync("/api/v1/transferencias", new
        {
            contaBancariaOrigemId = origemId,
            contaBancariaDestinoId = destinoId,
            valor = 300m,
            dataTransferencia = "2026-01-10"
        });
        await client.PostAsJsonAsync("/api/v1/transferencias", new
        {
            contaBancariaOrigemId = origemId,
            contaBancariaDestinoId = destinoId,
            valor = 400m,
            dataTransferencia = "2026-06-10"
        });

        var lista = await client.GetFromJsonAsync<TransferenciaListResponse>(
            "/api/v1/transferencias?dataInicial=2026-06-01&dataFinal=2026-06-30");

        lista.Should().NotBeNull();
        lista!.Items.Should().HaveCount(1);
        lista.Items[0].Valor.Should().Be(400m);
    }
}

internal record ContaBancariaTransfResponse(Guid Id, string Nome);
