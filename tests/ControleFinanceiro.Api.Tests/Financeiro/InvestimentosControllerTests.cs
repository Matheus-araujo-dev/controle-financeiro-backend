using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Contracts.Financeiro.Investimentos;
using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class InvestimentosControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static async Task<Guid> CriarContaBancariaAsync(HttpClient client, string nome = "Conta Investimento")
    {
        var resp = await client.PostAsJsonAsync("/api/v1/contas-bancarias", new
        {
            nome,
            banco = "Banco Investimento",
            saldoInicial = 0m,
            dataSaldoInicial = "2026-01-01"
        });
        resp.EnsureSuccessStatusCode();
        var obj = await resp.Content.ReadFromJsonAsync<ContaBancariaMinResponse>();
        return obj!.Id;
    }

    [Fact]
    public async Task Post_DeveCriarInvestimentoERetornarCreated()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var contaId = await CriarContaBancariaAsync(client);

        var resp = await client.PostAsJsonAsync("/api/v1/investimentos", new
        {
            nome = "Tesouro Selic 2029",
            emissor = "Tesouro Nacional",
            tipo = (int)TipoInvestimento.RendaFixa,
            liquidez = (int)LiquidezInvestimento.Diaria,
            valorInvestido = 10000m,
            dataAplicacao = "2026-01-15",
            dataVencimento = "2029-03-01",
            taxaAnual = 10.5m,
            contaBancariaVinculadaId = contaId
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var inv = await resp.Content.ReadFromJsonAsync<InvestimentoResumoResponse>(JsonOpts);
        inv.Should().NotBeNull();
        inv!.Nome.Should().Be("Tesouro Selic 2029");
        inv.ValorInvestido.Should().Be(10000m);
        inv.ValorAtual.Should().Be(10000m);
        inv.Encerrado.Should().BeFalse();
    }

    [Fact]
    public async Task Get_DeveListarInvestimentos()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var contaId = await CriarContaBancariaAsync(client);
        await client.PostAsJsonAsync("/api/v1/investimentos", new
        {
            nome = "CDB Banco X",
            tipo = (int)TipoInvestimento.RendaFixa,
            liquidez = (int)LiquidezInvestimento.Vencimento,
            valorInvestido = 5000m,
            dataAplicacao = "2026-03-01",
            contaBancariaVinculadaId = contaId
        });

        var lista = await client.GetFromJsonAsync<InvestimentoListResponse>("/api/v1/investimentos", JsonOpts);

        lista.Should().NotBeNull();
        lista!.Items.Should().HaveCount(1);
        lista.Items[0].Nome.Should().Be("CDB Banco X");
    }

    [Fact]
    public async Task GetById_DeveRetornarInvestimentoCriado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var contaId = await CriarContaBancariaAsync(client);
        var createResp = await client.PostAsJsonAsync("/api/v1/investimentos", new
        {
            nome = "LCI Caixa",
            tipo = (int)TipoInvestimento.RendaFixa,
            liquidez = (int)LiquidezInvestimento.Vencimento,
            valorInvestido = 20000m,
            dataAplicacao = "2026-02-01",
            contaBancariaVinculadaId = contaId
        });
        var criado = await createResp.Content.ReadFromJsonAsync<InvestimentoResumoResponse>(JsonOpts);

        var detail = await client.GetFromJsonAsync<InvestimentoResumoResponse>($"/api/v1/investimentos/{criado!.Id}", JsonOpts);

        detail.Should().NotBeNull();
        detail!.Nome.Should().Be("LCI Caixa");
    }

    [Fact]
    public async Task GetById_QuandoNaoEncontrado_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.GetAsync($"/api/v1/investimentos/{Guid.NewGuid()}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_DeveAtualizarDadosDoInvestimento()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var contaId = await CriarContaBancariaAsync(client);
        var createResp = await client.PostAsJsonAsync("/api/v1/investimentos", new
        {
            nome = "Investimento Original",
            tipo = (int)TipoInvestimento.RendaVariavel,
            liquidez = (int)LiquidezInvestimento.Diaria,
            valorInvestido = 3000m,
            dataAplicacao = "2026-04-01",
            contaBancariaVinculadaId = contaId
        });
        var criado = await createResp.Content.ReadFromJsonAsync<InvestimentoResumoResponse>(JsonOpts);

        var updateResp = await client.PutAsJsonAsync($"/api/v1/investimentos/{criado!.Id}", new
        {
            nome = "Investimento Atualizado",
            tipo = (int)TipoInvestimento.RendaVariavel,
            liquidez = (int)LiquidezInvestimento.Diaria
        });

        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var atualizado = await updateResp.Content.ReadFromJsonAsync<InvestimentoResumoResponse>(JsonOpts);
        atualizado!.Nome.Should().Be("Investimento Atualizado");
    }

    [Fact]
    public async Task Post_AtualizarValorAtual_DeveRegistrarNovoValor()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var contaId = await CriarContaBancariaAsync(client);
        var createResp = await client.PostAsJsonAsync("/api/v1/investimentos", new
        {
            nome = "Ações XPTO",
            tipo = (int)TipoInvestimento.RendaVariavel,
            liquidez = (int)LiquidezInvestimento.Diaria,
            valorInvestido = 1000m,
            dataAplicacao = "2026-01-01",
            contaBancariaVinculadaId = contaId
        });
        var criado = await createResp.Content.ReadFromJsonAsync<InvestimentoResumoResponse>(JsonOpts);

        var atualizarResp = await client.PostAsJsonAsync(
            $"/api/v1/investimentos/{criado!.Id}/atualizar-valor",
            new { valorAtual = 1250m });

        atualizarResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var inv = await atualizarResp.Content.ReadFromJsonAsync<InvestimentoResumoResponse>(JsonOpts);
        inv!.ValorAtual.Should().Be(1250m);
        inv.Rendimento.Should().Be(250m);
    }

    [Fact]
    public async Task Post_Encerrar_DeveMarcarComoEncerrado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var contaId = await CriarContaBancariaAsync(client);
        var createResp = await client.PostAsJsonAsync("/api/v1/investimentos", new
        {
            nome = "Poupança Extra",
            tipo = (int)TipoInvestimento.RendaFixa,
            liquidez = (int)LiquidezInvestimento.Diaria,
            valorInvestido = 500m,
            dataAplicacao = "2026-01-01",
            contaBancariaVinculadaId = contaId
        });
        var criado = await createResp.Content.ReadFromJsonAsync<InvestimentoResumoResponse>(JsonOpts);

        var encerrarResp = await client.PostAsJsonAsync(
            $"/api/v1/investimentos/{criado!.Id}/encerrar",
            new { valorResgate = 520m });

        encerrarResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var inv = await encerrarResp.Content.ReadFromJsonAsync<InvestimentoResumoResponse>(JsonOpts);
        inv!.Encerrado.Should().BeTrue();
        inv.ValorAtual.Should().Be(520m);
    }

    [Fact]
    public async Task Get_IndicadoresBcb_DeveRetornarOk()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/v1/investimentos/indicadores-bcb");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var indicadores = await resp.Content.ReadFromJsonAsync<IndicadoresBcbResponse>();
        indicadores.Should().NotBeNull();
    }

    [Fact]
    public async Task Get_FiltroEncerrado_DeveRetornarSomenteAtivos()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var contaId = await CriarContaBancariaAsync(client);
        var ativo = await (await client.PostAsJsonAsync("/api/v1/investimentos", new
        {
            nome = "Ativo",
            tipo = (int)TipoInvestimento.RendaFixa,
            liquidez = (int)LiquidezInvestimento.Diaria,
            valorInvestido = 1000m,
            dataAplicacao = "2026-01-01",
            contaBancariaVinculadaId = contaId
        })).Content.ReadFromJsonAsync<InvestimentoResumoResponse>(JsonOpts);

        var encerrado = await (await client.PostAsJsonAsync("/api/v1/investimentos", new
        {
            nome = "Encerrado",
            tipo = (int)TipoInvestimento.RendaFixa,
            liquidez = (int)LiquidezInvestimento.Diaria,
            valorInvestido = 2000m,
            dataAplicacao = "2026-01-01",
            contaBancariaVinculadaId = contaId
        })).Content.ReadFromJsonAsync<InvestimentoResumoResponse>(JsonOpts);

        await client.PostAsJsonAsync($"/api/v1/investimentos/{encerrado!.Id}/encerrar", new { valorResgate = 2100m });

        var listaAtivos = await client.GetFromJsonAsync<InvestimentoListResponse>("/api/v1/investimentos?encerrado=false", JsonOpts);
        listaAtivos!.Items.Should().HaveCount(1);
        listaAtivos.Items[0].Id.Should().Be(ativo!.Id);
    }
}

internal record ContaBancariaMinResponse(Guid Id, string Nome);
