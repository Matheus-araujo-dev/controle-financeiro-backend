using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

/// <summary>
/// Testes de integração para fluxos de recorrências não cobertos em RecorrenciasControllerTests:
/// Encerrar (com e sem pausa prévia), GET por id e Gerar ocorrências.
/// </summary>
public sealed class RecorrenciasFluxoTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    // ── Encerrar ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Encerrar_AposHaverPausado_DeveMarcarComoEncerrada()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var regraId = await CriarRecorrenciaContaPagarAsync(client, fixture,
            descricao: "Aluguel para encerrar",
            diaOrdemMensal: 10,
            dataFim: "2026-12-01");

        // 1. Pausar primeiro (regra de negócio: deve estar inativa para encerrar)
        var pausarResponse = await client.PostAsync($"/api/v1/recorrencias/{regraId}/pausar", content: null);
        pausarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Encerrar
        var encerrarResponse = await client.PostAsync($"/api/v1/recorrencias/{regraId}/encerrar", content: null);
        encerrarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var encerrada = await encerrarResponse.Content.ReadFromJsonAsync<RecorrenciaItemResponse>();
        encerrada.Should().NotBeNull();
        encerrada!.Ativa.Should().BeFalse();
        encerrada.Encerrada.Should().BeTrue();
    }

    [Fact]
    public async Task Encerrar_SemPausarAntes_DeveRetornarErro()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var regraId = await CriarRecorrenciaContaPagarAsync(client, fixture,
            descricao: "Aluguel sem pausar",
            diaOrdemMensal: 15,
            dataFim: "2026-12-01");

        // Tentar encerrar sem pausar — regra ativa
        var encerrarResponse = await client.PostAsync($"/api/v1/recorrencias/{regraId}/encerrar", content: null);

        // Deve retornar erro (400 ou 422 dependendo do middleware de validação)
        encerrarResponse.IsSuccessStatusCode.Should().BeFalse();
        ((int)encerrarResponse.StatusCode).Should().BeInRange(400, 499);
    }

    [Fact]
    public async Task Encerrar_RecorrenciaJaEncerrada_DeveRetornarOkIdempotente()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var regraId = await CriarRecorrenciaContaPagarAsync(client, fixture,
            descricao: "Aluguel duplo encerrar",
            diaOrdemMensal: 10,
            dataFim: "2026-12-01");

        // Pausar → Encerrar
        await client.PostAsync($"/api/v1/recorrencias/{regraId}/pausar", content: null);
        await client.PostAsync($"/api/v1/recorrencias/{regraId}/encerrar", content: null);

        // Encerrar novamente — a lógica tem if (!regra.Encerrada), deve ser idempotente
        var segundoEncerrar = await client.PostAsync($"/api/v1/recorrencias/{regraId}/encerrar", content: null);
        segundoEncerrar.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await segundoEncerrar.Content.ReadFromJsonAsync<RecorrenciaItemResponse>();
        resultado!.Encerrada.Should().BeTrue();
    }

    // ── Retomar após encerrar ──────────────────────────────────────────────

    [Fact]
    public async Task Retomar_RecorrenciaEncerrada_DeveRetornarErro()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var regraId = await CriarRecorrenciaContaPagarAsync(client, fixture,
            descricao: "Aluguel encerrado retomar",
            diaOrdemMensal: 10,
            dataFim: "2026-12-01");

        // Pausar → Encerrar
        await client.PostAsync($"/api/v1/recorrencias/{regraId}/pausar", content: null);
        await client.PostAsync($"/api/v1/recorrencias/{regraId}/encerrar", content: null);

        // Tentar retomar — recorrência encerrada não pode ser retomada
        var retomarResponse = await client.PostAsync($"/api/v1/recorrencias/{regraId}/retomar", content: null);

        retomarResponse.IsSuccessStatusCode.Should().BeFalse();
        ((int)retomarResponse.StatusCode).Should().BeInRange(400, 499);
    }

    // ── GET por id ────────────────────────────────────────────────────────

    [Fact]
    public async Task Obter_RecorrenciaExistente_DeveRetornarTodosOsCampos()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var regraId = await CriarRecorrenciaContaPagarAsync(client, fixture,
            descricao: "Aluguel para obter",
            diaOrdemMensal: 8,
            dataFim: "2026-08-01");

        var response = await client.GetAsync($"/api/v1/recorrencias/{regraId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var item = await response.Content.ReadFromJsonAsync<RecorrenciaItemResponse>();
        item.Should().NotBeNull();
        item!.Id.Should().Be(regraId);
        item.ContaOrigemTipo.Should().Be("ContaPagar");
        item.Descricao.Should().Be("Aluguel para obter");
        item.ValorLiquido.Should().Be(450m);
        item.PessoaNome.Should().Be("Fornecedor Fase 3");
        item.DiaOrdemMensal.Should().Be(8);
        item.Ativa.Should().BeTrue();
        item.Encerrada.Should().BeFalse();
        item.PermiteEdicaoOcorrenciaIndividual.Should().BeTrue();
    }

    [Fact]
    public async Task Obter_RecorrenciaContaReceber_DeveRetornarDadosDePagador()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var regraId = await CriarRecorrenciaContaReceberAsync(client, fixture,
            descricao: "Consultoria para obter",
            diaOrdemMensal: 15,
            dataFim: "2026-09-01");

        var response = await client.GetAsync($"/api/v1/recorrencias/{regraId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var item = await response.Content.ReadFromJsonAsync<RecorrenciaItemResponse>();
        item.Should().NotBeNull();
        item!.ContaOrigemTipo.Should().Be("ContaReceber");
        item.Descricao.Should().Be("Consultoria para obter");
        item.ValorLiquido.Should().Be(1200m);
        item.PessoaNome.Should().Be("Cliente Fase 3");
    }

    [Fact]
    public async Task Obter_IdInexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        var response = await client.GetAsync($"/api/v1/recorrencias/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Gerar ocorrências ─────────────────────────────────────────────────

    [Fact]
    public async Task GerarOcorrencias_ComRecorrenciaAtiva_DeveRetornarContagemNoResponse()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        await CriarRecorrenciaContaPagarAsync(client, fixture,
            descricao: "Aluguel recorrente",
            diaOrdemMensal: 20,
            dataFim: "2026-12-01");

        var response = await client.PostAsync("/api/v1/recorrencias/gerar-ocorrencias", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<GerarOcorrenciasResponse>();
        resultado.Should().NotBeNull();
        resultado!.RegrasEncontradas.Should().BeGreaterOrEqualTo(1);
        resultado.RegrasProcessadas.Should().BeGreaterOrEqualTo(1);
        resultado.Erros.Should().Be(0);
    }

    [Fact]
    public async Task GerarOcorrencias_SemRecorrencias_DeveRetornarZeros()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        var response = await client.PostAsync("/api/v1/recorrencias/gerar-ocorrencias", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<GerarOcorrenciasResponse>();
        resultado.Should().NotBeNull();
        resultado!.RegrasEncontradas.Should().Be(0);
        resultado.RegrasProcessadas.Should().Be(0);
        resultado.OcorrenciasGeradas.Should().Be(0);
        resultado.Erros.Should().Be(0);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static async Task<Guid> CriarRecorrenciaContaPagarAsync(
        HttpClient client,
        FinancialFixtureSeed.FixtureIds fixture,
        string descricao,
        int diaOrdemMensal,
        string dataFim)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            recebedorId = fixture.RecebedorId,
            responsavelCompraId = fixture.ResponsavelId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 450m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao,
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 450m } },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                tipoDia = "DiaFixo",
                diaOrdemMensal,
                dataInicio = (string?)null,
                dataFim,
                permiteEdicaoOcorrenciaIndividual = true,
                observacao = (string?)null
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "a criação da conta a pagar com recorrência deveria ter sucesso");
        var criada = await response.Content.ReadFromJsonAsync<ContaDetalheResponse>();
        return criada!.Recorrencia!.Id;
    }

    private static async Task<Guid> CriarRecorrenciaContaReceberAsync(
        HttpClient client,
        FinancialFixtureSeed.FixtureIds fixture,
        string descricao,
        int diaOrdemMensal,
        string dataFim)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-05",
            responsavelId = fixture.ResponsavelId,
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-15",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 1200m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao,
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 1200m } },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                tipoDia = "DiaFixo",
                diaOrdemMensal,
                dataInicio = (string?)null,
                dataFim,
                permiteEdicaoOcorrenciaIndividual = false,
                observacao = (string?)null
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "a criação da conta a receber com recorrência deveria ter sucesso");
        var criada = await response.Content.ReadFromJsonAsync<ContaDetalheResponse>();
        return criada!.Recorrencia!.Id;
    }

    // ── DTOs locais para deserialização ───────────────────────────────────

    private sealed record ContaDetalheResponse(Guid Id, RecorrenciaRef? Recorrencia);

    private sealed record RecorrenciaRef(Guid Id);

    private sealed record RecorrenciaItemResponse(
        Guid Id,
        string TipoPeriodicidade,
        string TipoDia,
        int DiaOrdemMensal,
        DateOnly DataInicio,
        DateOnly? DataFim,
        bool Ativa,
        bool PermiteEdicaoOcorrenciaIndividual,
        string? Observacao,
        string ContaOrigemTipo,
        Guid ContaOrigemId,
        string Descricao,
        decimal ValorLiquido,
        string PessoaNome,
        string? ResponsavelNome,
        bool Encerrada = false);

    private sealed record GerarOcorrenciasResponse(
        int RegrasEncontradas,
        int RegrasProcessadas,
        int OcorrenciasGeradas,
        int Erros);
}
