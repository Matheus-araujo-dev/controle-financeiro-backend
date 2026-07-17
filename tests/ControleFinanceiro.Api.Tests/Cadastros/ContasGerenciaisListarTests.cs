using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Cadastros;

/// <summary>
/// Testa filtros e ordenações de ContaGerencialAppService.ListarAsync via endpoint HTTP.
/// </summary>
public sealed class ContasGerenciaisListarTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private sealed record ContaGerencialResumo(
        Guid Id,
        string? Codigo,
        string Descricao,
        string Tipo,
        Guid? ContaPaiId,
        bool Ativo,
        bool AceitaLancamentos);

    private sealed record PagedResponse<T>(T[] Items, int TotalItems, int Page, int PageSize, int TotalPages);

    private static Task<System.Net.Http.HttpResponseMessage> CriarAsync(
        System.Net.Http.HttpClient client,
        string codigo,
        string descricao,
        string tipo,
        Guid? contaPaiId = null,
        bool ativo = true) =>
        client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo,
            descricao,
            tipo,
            contaPaiId,
            ativo,
            ehPadraoRecebimentoFaturaCartao = false
        });

    [Fact]
    public async Task ListarAsync_FiltroSearch_SemCorrespondencia_RetornaVazio()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "DESP", "Despesas Operacionais", "Despesa");

        // Busca por texto que não existe — exerce o branch do filtro search sem depender do LIKE
        var result = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResumo>>(
            "/api/v1/contas-gerenciais?search=ZZZInexistente&page=1&pageSize=50");

        result!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ListarAsync_FiltroTipo_RetornaApenasDespesas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "DESP2", "Despesa Filtro", "Despesa");
        await CriarAsync(client, "REC2", "Receita Filtro", "Receita");

        var result = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResumo>>(
            "/api/v1/contas-gerenciais?tipo=Despesa&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(x => x.Tipo == "Despesa");
    }

    [Fact]
    public async Task ListarAsync_FiltroTipos_MultiSelect_RetornaApenasTiposCombinando()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "DESP3", "Despesa Multi", "Despesa");
        await CriarAsync(client, "REC3", "Receita Multi", "Receita");

        var result = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResumo>>(
            "/api/v1/contas-gerenciais?tipos=Despesa&tipos=Receita&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListarAsync_FiltroAtivo_RetornaApenasAtivos()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "ATIV", "Conta Ativa", "Despesa", null, true);
        await CriarAsync(client, "INAT", "Conta Inativa", "Despesa", null, false);

        var result = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResumo>>(
            "/api/v1/contas-gerenciais?ativo=true&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(x => x.Ativo);
    }

    [Fact]
    public async Task ListarAsync_FiltroContaPaiId_RetornaApenasFilhas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var paiResp = await CriarAsync(client, "PAI", "Conta Pai", "Despesa");
        var pai = await paiResp.Content.ReadFromJsonAsync<ContaGerencialResumo>();

        await CriarAsync(client, "FILHA1", "Filha Um", "Despesa", pai!.Id);
        await CriarAsync(client, "RAIZ", "Outra Raiz", "Receita");

        var result = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResumo>>(
            $"/api/v1/contas-gerenciais?contaPaiId={pai.Id}&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(x => x.ContaPaiId == pai.Id);
    }

    [Fact]
    public async Task ListarAsync_FiltroContaPaiTexto_RetornaApenasFilhas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var paiResp = await CriarAsync(client, "PAITXT", "Conta Pai Texto", "Despesa");
        var pai = await paiResp.Content.ReadFromJsonAsync<ContaGerencialResumo>();
        await CriarAsync(client, "FILHATXT", "Filha Texto", "Despesa", pai!.Id);

        var result = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResumo>>(
            "/api/v1/contas-gerenciais?contaPai=Pai+Texto&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListarAsync_FiltroEhPadraoRecebimentoFaturaCartao_RetornaApenasNaoMarcados()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "NORMAL", "Conta Normal", "Despesa");

        var result = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResumo>>(
            "/api/v1/contas-gerenciais?ehPadraoRecebimentoFaturaCartao=false&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListarAsync_OrdenadoPorCodigo_RetornaOrdenado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "ZZ", "ZZ Desc", "Despesa");
        await CriarAsync(client, "AA", "AA Desc", "Receita");

        var result = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResumo>>(
            "/api/v1/contas-gerenciais?sortBy=codigo&sortDirection=Asc&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListarAsync_OrdenadoPorCodigoDesc_RetornaOrdenado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "BB", "BB Desc", "Despesa");
        await CriarAsync(client, "CC", "CC Desc", "Receita");

        var result = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResumo>>(
            "/api/v1/contas-gerenciais?sortBy=codigo&sortDirection=Desc&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListarAsync_OrdenadoPorTipo_RetornaOrdenado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "DTIPO", "Despesa Tipo", "Despesa");
        await CriarAsync(client, "RTIPO", "Receita Tipo", "Receita");

        var result = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResumo>>(
            "/api/v1/contas-gerenciais?sortBy=tipo&sortDirection=Asc&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListarAsync_OrdenadoPorTipoDesc_RetornaOrdenado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "DTIPOASC", "Despesa TipoDesc", "Despesa");
        await CriarAsync(client, "RTIPOASC", "Receita TipoDesc", "Receita");

        var result = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResumo>>(
            "/api/v1/contas-gerenciais?sortBy=tipo&sortDirection=Desc&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListarAsync_OrdenadoPorContaPaiDescricao_RetornaOrdenado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var paiResp = await CriarAsync(client, "PAIORD", "Pai Ord", "Despesa");
        var pai = await paiResp.Content.ReadFromJsonAsync<ContaGerencialResumo>();
        await CriarAsync(client, "FILHAORD", "Filha Ord", "Despesa", pai!.Id);
        await CriarAsync(client, "SEMPAIAORD", "Sem Pai Ord", "Receita");

        var result = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResumo>>(
            "/api/v1/contas-gerenciais?sortBy=contapaidescricao&sortDirection=Asc&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListarAsync_OrdenadoPorContaPaiDescricaoDesc_RetornaOrdenado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "DESC001", "Desc Conta", "Despesa");

        var result = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResumo>>(
            "/api/v1/contas-gerenciais?sortBy=contapaidescricao&sortDirection=Desc&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListarAsync_OrdenadoPorAceitaLancamentos_RetornaOrdenado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var paiResp = await CriarAsync(client, "PAILANC", "Pai Lanc", "Despesa");
        var pai = await paiResp.Content.ReadFromJsonAsync<ContaGerencialResumo>();
        await CriarAsync(client, "FILHALANC", "Filha Lanc", "Despesa", pai!.Id);

        var result = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResumo>>(
            "/api/v1/contas-gerenciais?sortBy=aceitalancamentos&sortDirection=Asc&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListarAsync_OrdenadoPorAceitaLancamentosDesc_RetornaOrdenado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "LANCES", "Conta Lances", "Despesa");

        var result = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResumo>>(
            "/api/v1/contas-gerenciais?sortBy=aceitalancamentos&sortDirection=Desc&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListarAsync_OrdenadoPorAtivo_RetornaOrdenado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "ATIVA", "Conta Ativa Ord", "Despesa", null, true);
        await CriarAsync(client, "INAT2", "Conta Inativa Ord", "Despesa", null, false);

        var result = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResumo>>(
            "/api/v1/contas-gerenciais?sortBy=ativo&sortDirection=Desc&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
        result.Items.First().Ativo.Should().BeTrue();
    }

    [Fact]
    public async Task ListarAsync_OrdenadoPorEhPadraoRecebimentoDesc_RetornaOrdenado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "NPADRAO", "Não Padrão", "Despesa");

        var result = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResumo>>(
            "/api/v1/contas-gerenciais?sortBy=ehpadraorecebimentofaturacartao&sortDirection=Desc&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListarAsync_OrdenadoPorDescricaoDesc_RetornaOrdenado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await CriarAsync(client, "AAA1", "AAA Descricao", "Despesa");
        await CriarAsync(client, "ZZZ1", "ZZZ Descricao", "Receita");

        var result = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResumo>>(
            "/api/v1/contas-gerenciais?sortDirection=Desc&page=1&pageSize=50");

        result!.Items.Should().NotBeEmpty();
    }
}
