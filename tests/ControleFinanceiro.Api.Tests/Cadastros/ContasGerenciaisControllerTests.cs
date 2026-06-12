using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Cadastros;

public sealed class ContasGerenciaisControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Get_QuandoFiltrarAceitaLancamentos_DeveRetornarApenasContasFilhasOuSemFilhos()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var parentResponse = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo = "DESP",
            descricao = "Despesas",
            tipo = "Despesa",
            ativo = true,
            ehPadraoRecebimentoFaturaCartao = false
        });

        var parent = await parentResponse.Content.ReadFromJsonAsync<ContaGerencialResponse>();

        var childResponse = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo = "DESP.OP",
            descricao = "Operacional",
            tipo = "Despesa",
            contaPaiId = parent!.Id,
            ativo = true,
            ehPadraoRecebimentoFaturaCartao = false
        });

        var standaloneResponse = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo = "REC.SRV",
            descricao = "Servicos",
            tipo = "Receita",
            ativo = true,
            ehPadraoRecebimentoFaturaCartao = false
        });

        var child = await childResponse.Content.ReadFromJsonAsync<ContaGerencialResponse>();
        var standalone = await standaloneResponse.Content.ReadFromJsonAsync<ContaGerencialResponse>();

        var filtered = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResponse>>("/api/v1/contas-gerenciais?aceitaLancamentos=true&page=1&pageSize=20");
        var allItems = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResponse>>("/api/v1/contas-gerenciais?page=1&pageSize=20");

        filtered.Should().NotBeNull();
        filtered!.Items.Select(item => item.Id).Should().BeEquivalentTo([child!.Id, standalone!.Id]);
        allItems.Should().NotBeNull();
        allItems!.Items.Should().ContainSingle(item => item.Id == parent.Id && item.AceitaLancamentos == false);
        allItems.Items.Should().ContainSingle(item => item.Id == child.Id && item.AceitaLancamentos);
    }

    [Fact]
    public async Task PostEPut_QuandoGerarCiclo_DeveRetornarErroDeValidacao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var parentResponse = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo = "DESP-ADM",
            descricao = "Administrativo",
            tipo = "Despesa",
            ativo = true,
            ehPadraoRecebimentoFaturaCartao = false
        });

        var parent = await parentResponse.Content.ReadFromJsonAsync<ContaGerencialResponse>();

        var childResponse = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo = "DESP-MKT",
            descricao = "Marketing",
            tipo = "Despesa",
            contaPaiId = parent!.Id,
            ativo = true,
            ehPadraoRecebimentoFaturaCartao = false
        });

        var child = await childResponse.Content.ReadFromJsonAsync<ContaGerencialResponse>();

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/contas-gerenciais/{parent.Id}", new
        {
            codigo = "DESP-ADM",
            descricao = "Administrativo",
            tipo = "Despesa",
            contaPaiId = child!.Id,
            ativo = true,
            ehPadraoRecebimentoFaturaCartao = false
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_QuandoContaFilhaTiverTipoDivergente_DeveHerdarTipoDaContaPai()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var parentResponse = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo = "REC",
            descricao = "Receitas",
            tipo = "Receita",
            ativo = true,
            ehPadraoRecebimentoFaturaCartao = false
        });

        var parent = await parentResponse.Content.ReadFromJsonAsync<ContaGerencialResponse>();

        var childResponse = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo = "REC.01",
            descricao = "Nova receita",
            tipo = "Despesa",
            contaPaiId = parent!.Id,
            ativo = true,
            ehPadraoRecebimentoFaturaCartao = false
        });

        childResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var child = await childResponse.Content.ReadFromJsonAsync<ContaGerencialResponse>();
        child.Should().NotBeNull();
        child!.Tipo.Should().Be("Receita");
    }

    [Fact]
    public async Task Post_QuandoMarcarPadraoRecebimentoFatura_DevePermitirApenasUmaContaDeReceita()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var primeiraResponse = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo = "REC.DIV",
            descricao = "Recebimento de divida",
            tipo = "Receita",
            ativo = true,
            ehPadraoRecebimentoFaturaCartao = true
        });

        primeiraResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var primeira = await primeiraResponse.Content.ReadFromJsonAsync<ContaGerencialResponse>();
        primeira.Should().NotBeNull();
        primeira!.EhPadraoRecebimentoFaturaCartao.Should().BeTrue();

        var segundaResponse = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo = "REC.OUTRA",
            descricao = "Outro recebimento",
            tipo = "Receita",
            ativo = true,
            ehPadraoRecebimentoFaturaCartao = true
        });

        segundaResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var despesaResponse = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo = "DESP.TESTE",
            descricao = "Despesa teste",
            tipo = "Despesa",
            ativo = true,
            ehPadraoRecebimentoFaturaCartao = true
        });

        despesaResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_QuandoInformarResponsavelPadrao_DevePersistirERetornarNoDetalheELista()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var pessoaResponse = await client.PostAsJsonAsync("/api/v1/pessoas", new
        {
            nome = "Matheus",
            tipoPessoa = "Fisica"
        });

        var pessoa = await pessoaResponse.Content.ReadFromJsonAsync<PessoaResponse>();

        var createResponse = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo = "DES.14",
            descricao = "Assinaturas",
            tipo = "Despesa",
            ativo = true,
            ehPadraoRecebimentoFaturaCartao = false,
            responsavelPadraoId = pessoa!.Id
        });

        var created = await createResponse.Content.ReadFromJsonAsync<ContaGerencialResponse>();
        var listResponse = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResponse>>("/api/v1/contas-gerenciais?page=1&pageSize=20");

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Should().NotBeNull();
        created!.ResponsavelPadraoId.Should().Be(pessoa.Id);
        created.ResponsavelPadraoNome.Should().Be("Matheus");
        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().ContainSingle(item =>
            item.Id == created.Id &&
            item.ResponsavelPadraoId == pessoa.Id &&
            item.ResponsavelPadraoNome == "Matheus");
    }

    [Fact]
    public async Task Post_QuandoResponsavelPadraoNaoExistir_DeveRetornarErroDeValidacao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo = "DES.15",
            descricao = "Teste",
            tipo = "Despesa",
            ativo = true,
            ehPadraoRecebimentoFaturaCartao = false,
            responsavelPadraoId = Guid.NewGuid()
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_QuandoOrdenarPorCodigo_DeveRespeitarOrdemAscendente()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        (await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo = "DES.10.01",
            descricao = "Tecnologia",
            tipo = "Despesa",
            ativo = true,
            ehPadraoRecebimentoFaturaCartao = false
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        (await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo = "DES.02.01",
            descricao = "Supermercado",
            tipo = "Despesa",
            ativo = true,
            ehPadraoRecebimentoFaturaCartao = false
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        (await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo = "REC.01",
            descricao = "Salário",
            tipo = "Receita",
            ativo = true,
            ehPadraoRecebimentoFaturaCartao = false
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await client.GetFromJsonAsync<PagedResponse<ContaGerencialResponse>>(
            "/api/v1/contas-gerenciais?page=1&pageSize=20&sortBy=codigo&sortDirection=Asc");

        listResponse.Should().NotBeNull();
        listResponse!.Items.Select(item => item.Codigo).Should().ContainInOrder("DES.02.01", "DES.10.01", "REC.01");
    }

    private sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

    private sealed record ContaGerencialResponse(
        Guid Id,
        string? Codigo,
        string Descricao,
        string Tipo,
        Guid? ContaPaiId,
        string? ContaPaiDescricao,
        bool Ativo,
        bool AceitaLancamentos,
        bool EhPadraoRecebimentoFaturaCartao,
        Guid? ResponsavelPadraoId,
        string? ResponsavelPadraoNome);

    private sealed record PessoaResponse(Guid Id, string Nome);
}
