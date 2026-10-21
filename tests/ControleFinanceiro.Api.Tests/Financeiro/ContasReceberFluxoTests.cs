using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class ContasReceberFluxoTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private sealed record ContaResumo(Guid Id, string StatusCodigo, string StatusNome);

    private static async Task<Guid> CriarContaReceberAsync(HttpClient client, FinancialFixtureSeed.FixtureIds fixture, decimal valor = 200m)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-04",
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = valor,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Receita fluxo",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialReceitaId, valor } }
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ContaResumo>();
        return created!.Id;
    }

    [Fact]
    public async Task LiquidarEEstornar_DeveVoltarStatusParaPendente()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarContaReceberAsync(client, fixture);

        var liquidar = await client.PostAsJsonAsync($"/api/v1/contas-receber/{id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId,
            valorLiquidacao = 200m,
            atualizarValorConta = true
        });
        liquidar.StatusCode.Should().Be(HttpStatusCode.OK);
        var liquidada = await liquidar.Content.ReadFromJsonAsync<ContaResumo>();
        liquidada!.StatusCodigo.Should().Be("LIQUIDADA");

        var estornar = await client.PostAsync($"/api/v1/contas-receber/{id}/estornar", content: null);
        estornar.StatusCode.Should().Be(HttpStatusCode.OK);
        var estornada = await estornar.Content.ReadFromJsonAsync<ContaResumo>();
        estornada!.StatusCodigo.Should().Be("PENDENTE");

        // Movimentação de entrada foi removida pelo estorno
        var movs = await client.GetFromJsonAsync<PagedResponse<MovimentacaoResumoResponse>>(
            "/api/v1/movimentacoes?search=Receita fluxo");
        movs!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancelar_DeveMarcarComoCancelada()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarContaReceberAsync(client, fixture);

        var cancelar = await client.PostAsync($"/api/v1/contas-receber/{id}/cancelar", content: null);

        cancelar.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelada = await cancelar.Content.ReadFromJsonAsync<ContaResumo>();
        cancelada!.StatusCodigo.Should().Be("CANCELADA");
    }

    [Fact]
    public async Task ObterPorId_DeveRetornarDetalhe_EInexistente404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarContaReceberAsync(client, fixture);

        var detalhe = await client.GetFromJsonAsync<ContaResumo>($"/api/v1/contas-receber/{id}");
        detalhe!.Id.Should().Be(id);

        var inexistente = await client.GetAsync($"/api/v1/contas-receber/{Guid.NewGuid()}");
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Estornar_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resposta = await client.PostAsync($"/api/v1/contas-receber/{Guid.NewGuid()}/estornar", content: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LiquidarParcial_DeveSetarStatusParcialEExporValorPago()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarContaReceberAsync(client, fixture, valor: 200m);

        var liquidar = await client.PostAsJsonAsync($"/api/v1/contas-receber/{id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId,
            valorLiquidacao = 120m,
            atualizarValorConta = false,
            cancelarValorRestante = false
        });

        liquidar.StatusCode.Should().Be(HttpStatusCode.OK);
        var detalhe = await liquidar.Content.ReadFromJsonAsync<ContaDetalhe>();
        detalhe!.StatusCodigo.Should().Be("PARCIAL");
        detalhe.ValorPago.Should().Be(120m);
        detalhe.ValorLiquido.Should().Be(200m);
    }

    [Fact]
    public async Task LiquidarParcial_CancelarRestante_DeveSetarLiquidada()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarContaReceberAsync(client, fixture, valor: 200m);

        var liquidar = await client.PostAsJsonAsync($"/api/v1/contas-receber/{id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId,
            valorLiquidacao = 120m,
            atualizarValorConta = false,
            cancelarValorRestante = true
        });

        liquidar.StatusCode.Should().Be(HttpStatusCode.OK);
        var detalhe = await liquidar.Content.ReadFromJsonAsync<ContaDetalhe>();
        detalhe!.StatusCodigo.Should().Be("LIQUIDADA");
        detalhe.ValorLiquido.Should().Be(120m);
    }

    [Fact]
    public async Task LiquidarParcial_Estornar_DeveVoltarParaPendente()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarContaReceberAsync(client, fixture, valor: 200m);

        await client.PostAsJsonAsync($"/api/v1/contas-receber/{id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId,
            valorLiquidacao = 120m,
            atualizarValorConta = false,
            cancelarValorRestante = false
        });

        var estornar = await client.PostAsync($"/api/v1/contas-receber/{id}/estornar", content: null);

        estornar.StatusCode.Should().Be(HttpStatusCode.OK);
        (await estornar.Content.ReadFromJsonAsync<ContaResumo>())!.StatusCodigo.Should().Be("PENDENTE");
    }

    [Fact]
    public async Task CancelarParcial_DeveTrimValueELiquidar()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarContaReceberAsync(client, fixture, valor: 200m);

        await client.PostAsJsonAsync($"/api/v1/contas-receber/{id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId,
            valorLiquidacao = 120m,
            atualizarValorConta = false,
            cancelarValorRestante = false
        });

        var cancelar = await client.PostAsync($"/api/v1/contas-receber/{id}/cancelar", content: null);

        cancelar.StatusCode.Should().Be(HttpStatusCode.OK);
        var detalhe = await cancelar.Content.ReadFromJsonAsync<ContaDetalhe>();
        detalhe!.StatusCodigo.Should().Be("LIQUIDADA");
        detalhe.ValorLiquido.Should().Be(120m);
    }

    private sealed record PagedResponse<T>(IReadOnlyCollection<T> Items);
    private sealed record MovimentacaoResumoResponse(Guid Id, decimal Valor);
    private sealed record ContaDetalhe(Guid Id, string StatusCodigo, decimal ValorLiquido, decimal? ValorPago);

    private sealed record ParcelaResumo(Guid Id, int NumeroParcela, string StatusCodigo);
    private sealed record ParcelaListResponse(IReadOnlyCollection<ParcelaResumo> Items);

    private static async Task<List<ParcelaResumo>> CriarParceladaAsync(HttpClient client, FinancialFixtureSeed.FixtureIds fixture, string descricao)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-04",
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 500m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 5,
            descricao,
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 500m } }
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ListarParcelasAsync(client, descricao);
    }

    private static async Task<List<ParcelaResumo>> ListarParcelasAsync(HttpClient client, string descricao)
    {
        var lista = await client.GetFromJsonAsync<ParcelaListResponse>($"/api/v1/contas-receber?descricao={Uri.EscapeDataString(descricao)}&pageSize=50");
        return lista!.Items.OrderBy(x => x.NumeroParcela).ToList();
    }

    [Theory]
    [InlineData("true", new[] { "CANCELADA", "CANCELADA", "CANCELADA", "CANCELADA", "CANCELADA" })]
    [InlineData("null", new[] { "CANCELADA", "CANCELADA", "CANCELADA", "CANCELADA", "CANCELADA" })]
    [InlineData("false", new[] { "CANCELADA", "VENCIDA", "VENCIDA", "VENCIDA", "VENCIDA" })]
    public async Task CancelarPrimeiraParcela_DeveRespeitarEscolhaDeCascata(string cancelarFuturas, string[] statusEsperados)
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var parcelas = await CriarParceladaAsync(client, fixture, "Parcelado cascata");
        parcelas.Should().HaveCount(5);

        var body = JsonContent.Create(new { cancelarParcelasFuturas = cancelarFuturas == "null" ? (bool?)null : bool.Parse(cancelarFuturas) });
        var cancelar = await client.PostAsync($"/api/v1/contas-receber/{parcelas[0].Id}/cancelar", body);
        cancelar.StatusCode.Should().Be(HttpStatusCode.OK);

        var depois = await ListarParcelasAsync(client, "Parcelado cascata");
        depois.Select(x => x.StatusCodigo).Should().Equal(statusEsperados);
    }

    [Fact]
    public async Task CancelarParcelaIntermediaria_ComCascata_DeveCancelarApenasAsSeguintes()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var parcelas = await CriarParceladaAsync(client, fixture, "Parcelado meio");

        var cancelar = await client.PostAsJsonAsync($"/api/v1/contas-receber/{parcelas[2].Id}/cancelar", new { cancelarParcelasFuturas = true });
        cancelar.StatusCode.Should().Be(HttpStatusCode.OK);

        var depois = await ListarParcelasAsync(client, "Parcelado meio");
        depois.Select(x => x.StatusCodigo).Should().Equal("VENCIDA", "VENCIDA", "CANCELADA", "CANCELADA", "CANCELADA");
    }

    [Fact]
    public async Task CancelarParcelaIntermediaria_SemCascata_DeveCancelarApenasEla()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var parcelas = await CriarParceladaAsync(client, fixture, "Parcelado unico");

        var cancelar = await client.PostAsync($"/api/v1/contas-receber/{parcelas[2].Id}/cancelar", content: null);
        cancelar.StatusCode.Should().Be(HttpStatusCode.OK);

        var depois = await ListarParcelasAsync(client, "Parcelado unico");
        depois.Select(x => x.StatusCodigo).Should().Equal("VENCIDA", "VENCIDA", "CANCELADA", "VENCIDA", "VENCIDA");
    }
}
