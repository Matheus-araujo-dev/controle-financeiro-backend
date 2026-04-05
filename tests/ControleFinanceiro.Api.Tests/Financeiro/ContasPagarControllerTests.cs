using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class ContasPagarControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Post_DeveCriarContaParceladaEListarParcelas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            numeroDocumento = "NF-2026-1",
            dataEmissao = "2026-04-04",
            responsavelCompraId = fixture.ResponsavelId,
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            cartaoId = (string?)null,
            contaBancariaId = (string?)null,
            valorOriginal = 100.00m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0.01m,
            quantidadeParcelas = 3,
            descricao = "Servico parcelado",
            observacao = "Observacao de teste",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 60.01m },
                new { contaGerencialId = fixture.ContaGerencialAdministrativaId, valor = 40m }
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();
        var listResponse = await client.GetFromJsonAsync<PagedResponse<ContaResumoResponse>>("/api/v1/contas-pagar?search=Servico");

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Should().NotBeNull();
        created!.NumeroParcela.Should().Be(1);
        created.QuantidadeParcelas.Should().Be(3);
        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().HaveCount(3);
        listResponse.Items.Sum(item => item.ValorLiquido).Should().Be(100.01m);
    }

    [Fact]
    public async Task Post_QuandoFormaPagamentoBaixaAutomaticamenteSemContaBancaria_DeveRetornarBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoAutoId,
            valorOriginal = 150m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Baixa automatica sem conta",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 150m }
            }
        });

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Errors.Should().ContainKey("ContaBancariaId");
    }

    [Fact]
    public async Task PostLiquidar_DeveGerarMovimentacaoDeSaida()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 120m,
            valorDesconto = 20m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta para liquidar",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 100m }
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        var liquidarResponse = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{created!.Id}/liquidar", new
        {
            dataLiquidacao = "2026-04-06",
            contaBancariaId = fixture.ContaBancariaId
        });

        var movimento = await client.GetFromJsonAsync<PagedResponse<MovimentacaoResumoResponse>>("/api/v1/movimentacoes?search=Conta para liquidar");

        liquidarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        movimento.Should().NotBeNull();
        movimento!.Items.Should().ContainSingle(item =>
            item.Tipo == "Saida" &&
            item.Natureza == "Realizada" &&
            item.ContaPagarId == created.Id &&
            item.Valor == 100m);
    }

    [Fact]
    public async Task Post_ComRecorrenciaDeveCriarRegraEPermitirGerarOcorrencias()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 90m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Internet escritorio",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 90m }
            },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                diaGeracaoMensal = 20,
                dataInicio = "2026-04-20",
                dataFim = (string?)null,
                permiteEdicaoOcorrenciaIndividual = true,
                observacao = "Contrato mensal"
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        var gerarResponse = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{created!.Id}/gerar-ocorrencias", new
        {
            ateData = "2026-06-30"
        });

        var listResponse = await client.GetFromJsonAsync<PagedResponse<ContaResumoResponse>>("/api/v1/contas-pagar?search=Internet");

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Should().NotBeNull();
        created!.Recorrencia.Should().NotBeNull();
        created.Recorrencia!.TipoPeriodicidade.Should().Be("Mensal");
        created.EhRecorrente.Should().BeTrue();
        gerarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().HaveCount(3);
        listResponse.Items.Should().OnlyContain(item => item.EhRecorrente);
    }

    [Fact]
    public async Task PostAlterarFuturas_DevePreservarOcorrenciaAnterior()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 120m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Licenca SaaS",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 120m }
            },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                diaGeracaoMensal = 20,
                dataInicio = "2026-04-20",
                dataFim = (string?)null,
                permiteEdicaoOcorrenciaIndividual = true
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        var gerarResponse = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{created!.Id}/gerar-ocorrencias", new
        {
            ateData = "2026-07-31"
        });

        gerarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listResponse = await client.GetFromJsonAsync<PagedResponse<ContaResumoResponse>>("/api/v1/contas-pagar?search=Licenca");
        var junho = listResponse!.Items.Single(item => item.DataVencimento == new DateOnly(2026, 6, 20));

        var alterarResponse = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{junho.Id}/alterar-futuras", new
        {
            dataEmissao = "2026-06-04",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-06-20",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 150m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Licenca SaaS reajustada",
            observacao = "Ajuste futuro",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 150m }
            },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                diaGeracaoMensal = 20,
                dataInicio = "2026-04-20",
                dataFim = (string?)null,
                permiteEdicaoOcorrenciaIndividual = true,
                observacao = "Regra ajustada"
            }
        });

        alterarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var abril = await client.GetFromJsonAsync<ContaDetalheResponse>($"/api/v1/contas-pagar/{created.Id}");
        var junhoAtualizado = await client.GetFromJsonAsync<ContaDetalheResponse>($"/api/v1/contas-pagar/{junho.Id}");
        var julho = (await client.GetFromJsonAsync<PagedResponse<ContaResumoResponse>>("/api/v1/contas-pagar?search=Licenca"))!
            .Items.Single(item => item.DataVencimento == new DateOnly(2026, 7, 20));

        abril!.Descricao.Should().Be("Licenca SaaS");
        abril.ValorLiquido.Should().Be(120m);
        junhoAtualizado!.Descricao.Should().Be("Licenca SaaS reajustada");
        junhoAtualizado.ValorLiquido.Should().Be(150m);
        julho.Descricao.Should().Be("Licenca SaaS reajustada");
        julho.ValorLiquido.Should().Be(150m);
    }

    [Fact]
    public async Task PostPausarRecorrencia_DeveImpedirNovaGeracao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-15",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 50m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Plataforma de apoio",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 50m }
            },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                diaGeracaoMensal = 15,
                dataInicio = "2026-04-15",
                dataFim = (string?)null,
                permiteEdicaoOcorrenciaIndividual = true
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        var pauseResponse = await client.PostAsync($"/api/v1/contas-pagar/{created!.Id}/pausar-recorrencia", null);
        var gerarResponse = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{created.Id}/gerar-ocorrencias", new
        {
            ateData = "2026-06-30"
        });

        var error = await gerarResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();

        pauseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        gerarResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Errors.Should().ContainKey("Recorrencia");
    }

    private sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

    private sealed record ApiErrorResponse(string Code, string Message, IReadOnlyDictionary<string, string[]> Errors, string TraceId);

    private sealed record ContaResumoResponse(
        Guid Id,
        string Descricao,
        decimal ValorLiquido,
        int QuantidadeParcelas,
        int NumeroParcela,
        DateOnly DataVencimento,
        bool EhRecorrente);

    private sealed record ContaDetalheResponse(
        Guid Id,
        string Descricao,
        decimal ValorLiquido,
        int QuantidadeParcelas,
        int NumeroParcela,
        bool EhRecorrente,
        RecorrenciaResponse? Recorrencia);

    private sealed record RecorrenciaResponse(
        Guid Id,
        string TipoPeriodicidade,
        int DiaGeracaoMensal,
        DateOnly DataInicio,
        DateOnly? DataFim,
        bool Ativa,
        bool PermiteEdicaoOcorrenciaIndividual,
        string? Observacao);

    private sealed record MovimentacaoResumoResponse(
        Guid Id,
        DateOnly DataMovimentacao,
        string Tipo,
        string Natureza,
        string StatusCodigo,
        decimal Valor,
        Guid? ContaPagarId,
        Guid? ContaReceberId,
        string? Observacao);
}
