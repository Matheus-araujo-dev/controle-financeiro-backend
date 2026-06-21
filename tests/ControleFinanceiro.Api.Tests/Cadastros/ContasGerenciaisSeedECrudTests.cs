using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Contracts.Cadastros.ContasGerenciais;
using ControleFinanceiro.Contracts.Common;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Cadastros;

public sealed class ContasGerenciaisSeedECrudTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    // A API serializa enums como string (JsonStringEnumConverter); o cliente precisa do mesmo converter.
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task SeedPlanoInicial_DeveCriarPlanoDeContas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var seed = await client.PostAsync("/api/v1/contas-gerenciais/seed-plano-inicial", content: null);
        seed.StatusCode.Should().Be(HttpStatusCode.OK);
        var resultado = await seed.Content.ReadFromJsonAsync<SeedPlanoInicialResponse>(Json);
        resultado!.ContasCriadas.Should().BeGreaterThan(0);

        var lista = await client.GetFromJsonAsync<PagedResult<ContaGerencialResumoResponse>>(
            "/api/v1/contas-gerenciais?pageSize=500", Json);
        lista!.TotalItems.Should().BeGreaterThanOrEqualTo(resultado.ContasCriadas);
    }

    [Fact]
    public async Task Crud_CriarObterAtualizar_DeveRefletirMudancas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var criar = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new CriarContaGerencialRequest(
            Codigo: "DESP-99",
            Descricao: "Despesa de teste",
            Tipo: ContaGerencialTipo.Despesa,
            ContaPaiId: null,
            ResponsavelPadraoId: null,
            Ativo: true,
            EhPadraoRecebimentoFaturaCartao: false));
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        var criada = await criar.Content.ReadFromJsonAsync<ContaGerencialDetalheResponse>(Json);
        criada!.Descricao.Should().Be("Despesa de teste");
        criada.AceitaLancamentos.Should().BeTrue();

        var detalhe = await client.GetFromJsonAsync<ContaGerencialDetalheResponse>(
            $"/api/v1/contas-gerenciais/{criada.Id}", Json);
        detalhe!.Codigo.Should().Be("DESP-99");

        var atualizar = await client.PutAsJsonAsync($"/api/v1/contas-gerenciais/{criada.Id}",
            new AtualizarContaGerencialRequest(
                Codigo: "DESP-99",
                Descricao: "Despesa renomeada",
                Tipo: ContaGerencialTipo.Despesa,
                ContaPaiId: null,
                ResponsavelPadraoId: null,
                Ativo: false,
                EhPadraoRecebimentoFaturaCartao: false));
        atualizar.StatusCode.Should().Be(HttpStatusCode.OK);
        var atualizada = await atualizar.Content.ReadFromJsonAsync<ContaGerencialDetalheResponse>(Json);
        atualizada!.Descricao.Should().Be("Despesa renomeada");
        atualizada.Ativo.Should().BeFalse();
    }

    [Fact]
    public async Task ObterPorId_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resposta = await client.GetAsync($"/api/v1/contas-gerenciais/{Guid.NewGuid()}");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Atualizar_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resposta = await client.PutAsJsonAsync($"/api/v1/contas-gerenciais/{Guid.NewGuid()}",
            new AtualizarContaGerencialRequest(
                Codigo: "X",
                Descricao: "Inexistente",
                Tipo: ContaGerencialTipo.Despesa,
                ContaPaiId: null,
                ResponsavelPadraoId: null,
                Ativo: true,
                EhPadraoRecebimentoFaturaCartao: false));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
