using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Cadastros;

/// <summary>
/// Testa filtros e ordenações de FormaPagamentoAppService.ListarAsync via endpoint HTTP.
/// </summary>
public sealed class FormasPagamentoListarTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private sealed record FormaPagamentoResumo(
        Guid Id,
        string Nome,
        string Tipo,
        bool EhCartao,
        bool BaixarAutomaticamente,
        bool Ativo);

    private sealed record PagedResponse<T>(T[] Items, int TotalItems, int Page, int PageSize, int TotalPages);

    private static Task<System.Net.Http.HttpResponseMessage> CriarAsync(
        System.Net.Http.HttpClient client, string nome, string tipo, bool ehCartao, bool baixarAuto, bool ativo) =>
        client.PostAsJsonAsync("/api/v1/formas-pagamento", new { nome, tipo, ehCartao, baixarAutomaticamente = baixarAuto, ativo });

    [Fact]
    public async Task ListarAsync_SemFiltro_RetornaTodos()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "Pix", "Pix", false, true, true);
        await CriarAsync(client, "Dinheiro", "Dinheiro", false, false, true);
        await CriarAsync(client, "Cartão Visa", "Credito", true, false, true);

        var result = await client.GetFromJsonAsync<PagedResponse<FormaPagamentoResumo>>(
            "/api/v1/formas-pagamento?page=1&pageSize=50");

        result!.Items.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task ListarAsync_FiltroSearch_RetornaApenasCombinando()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "Pix Empresa", "Pix", false, true, true);
        await CriarAsync(client, "Débito Automático", "Debito", false, true, true);

        var result = await client.GetFromJsonAsync<PagedResponse<FormaPagamentoResumo>>(
            "/api/v1/formas-pagamento?search=Pix&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(x => x.Nome.Contains("Pix", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ListarAsync_FiltroTipo_RetornaApenasTipoCorreto()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "Pix Corrente", "Pix", false, false, true);
        await CriarAsync(client, "Boleto Bancário", "Boleto", false, false, true);

        var result = await client.GetFromJsonAsync<PagedResponse<FormaPagamentoResumo>>(
            "/api/v1/formas-pagamento?tipo=Boleto&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(x => x.Tipo == "Boleto");
    }

    [Fact]
    public async Task ListarAsync_FiltroEhCartao_RetornaApenasCartoes()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "Mastercard", "Credito", true, false, true);
        await CriarAsync(client, "Pix Filtro", "Pix", false, true, true);

        var result = await client.GetFromJsonAsync<PagedResponse<FormaPagamentoResumo>>(
            "/api/v1/formas-pagamento?ehCartao=true&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(x => x.EhCartao);
    }

    [Fact]
    public async Task ListarAsync_FiltroBaixarAutomaticamente_RetornaApenasAuto()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "Débito Auto", "Debito", false, true, true);
        await CriarAsync(client, "Manual Pix", "Pix", false, false, true);

        var result = await client.GetFromJsonAsync<PagedResponse<FormaPagamentoResumo>>(
            "/api/v1/formas-pagamento?baixarAutomaticamente=true&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(x => x.BaixarAutomaticamente);
    }

    [Fact]
    public async Task ListarAsync_FiltroAtivo_RetornaApenasAtivos()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "Ativo Pix", "Pix", false, false, true);
        await CriarAsync(client, "Inativo Dinheiro", "Dinheiro", false, false, false);

        var result = await client.GetFromJsonAsync<PagedResponse<FormaPagamentoResumo>>(
            "/api/v1/formas-pagamento?ativo=true&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(x => x.Ativo);
    }

    [Fact]
    public async Task ListarAsync_OrdenadoPorTipo_RetornaOrdenado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "ZZ Pix", "Pix", false, false, true);
        await CriarAsync(client, "AA Dinheiro", "Dinheiro", false, false, true);

        var result = await client.GetFromJsonAsync<PagedResponse<FormaPagamentoResumo>>(
            "/api/v1/formas-pagamento?sortBy=tipo&sortDirection=Asc&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListarAsync_OrdenadoPorEhCartaoDesc_RetornaOrdenado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "Cartão Ord", "Credito", true, false, true);
        await CriarAsync(client, "Não Cartão", "Pix", false, false, true);

        var result = await client.GetFromJsonAsync<PagedResponse<FormaPagamentoResumo>>(
            "/api/v1/formas-pagamento?sortBy=ehcartao&sortDirection=Desc&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
        result.Items.First().EhCartao.Should().BeTrue();
    }

    [Fact]
    public async Task ListarAsync_OrdenadoPorBaixarAutomaticamenteDesc_RetornaOrdenado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "Auto Ord", "Debito", false, true, true);
        await CriarAsync(client, "Manual Ord", "Pix", false, false, true);

        var result = await client.GetFromJsonAsync<PagedResponse<FormaPagamentoResumo>>(
            "/api/v1/formas-pagamento?sortBy=baixarautomaticamente&sortDirection=Desc&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
        result.Items.First().BaixarAutomaticamente.Should().BeTrue();
    }

    [Fact]
    public async Task ListarAsync_OrdenadoPorAtivoDesc_RetornaOrdenado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "Ativo Ord", "Pix", false, false, true);
        await CriarAsync(client, "Inativo Ord", "Dinheiro", false, false, false);

        var result = await client.GetFromJsonAsync<PagedResponse<FormaPagamentoResumo>>(
            "/api/v1/formas-pagamento?sortBy=ativo&sortDirection=Desc&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
        result.Items.First().Ativo.Should().BeTrue();
    }

    [Fact]
    public async Task ListarAsync_OrdenadoPorNomeDesc_RetornaOrdenado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "AAA Nome", "Pix", false, false, true);
        await CriarAsync(client, "ZZZ Nome", "Dinheiro", false, false, true);

        var result = await client.GetFromJsonAsync<PagedResponse<FormaPagamentoResumo>>(
            "/api/v1/formas-pagamento?sortBy=nome&sortDirection=Desc&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListarAsync_FiltroTipos_MultiSelect_RetornaApenasTiposCombinando()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "Multi Pix", "Pix", false, false, true);
        await CriarAsync(client, "Multi Dinheiro", "Dinheiro", false, false, true);
        await CriarAsync(client, "Multi Credito", "Credito", true, false, true);

        var result = await client.GetFromJsonAsync<PagedResponse<FormaPagamentoResumo>>(
            "/api/v1/formas-pagamento?tipos=Pix&tipos=Dinheiro&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(x => x.Tipo == "Pix" || x.Tipo == "Dinheiro");
    }
}
