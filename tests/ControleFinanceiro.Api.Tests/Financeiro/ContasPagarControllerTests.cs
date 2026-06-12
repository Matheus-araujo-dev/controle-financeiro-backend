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
        var listResponse = await client.GetFromJsonAsync<ContaListResponse>("/api/v1/contas-pagar?search=Servico");

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Should().NotBeNull();
        created!.NumeroParcela.Should().Be(1);
        created.QuantidadeParcelas.Should().Be(3);
        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().HaveCount(3);
        listResponse.Items.Sum(item => item.ValorLiquido).Should().Be(100.01m);
    }

    [Fact]
    public async Task Get_ComFiltroDeStatusDeveRetornarSummaryFiltrado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        var liquidadaResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-01",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-07",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 81m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta liquidada",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 81m }
            }
        });

        var liquidada = await liquidadaResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-02",
            recebedorId = fixture.RecebedorId,
            dataVencimento = hoje.ToString("yyyy-MM-dd"),
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 120m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta pendente",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 120m }
            }
        });

        var liquidarResponse = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{liquidada!.Id}/liquidar", new
        {
            dataLiquidacao = "2026-04-07",
            contaBancariaId = fixture.ContaBancariaId
        });

        var listResponse = await client.GetFromJsonAsync<ContaListResponse>("/api/v1/contas-pagar?statusCodigo=LIQUIDADA");

        liquidarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().ContainSingle(item => item.Descricao == "Conta liquidada");
        listResponse.Summary.TotalRegistros.Should().Be(1);
        listResponse.Summary.ValorTotal.Should().Be(81m);
        listResponse.Summary.TotalPendente.Should().Be(120m);
        listResponse.Summary.TotalVencendoHoje.Should().Be(120m);
        listResponse.Summary.TotalLiquidado.Should().Be(81m);
    }

    [Fact]
    public async Task Get_ComMultiplosStatusDeveRetornarSummaryFiltrado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var liquidadaResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-01",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-07",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 81m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta liquidada",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 81m }
            }
        });

        var liquidada = await liquidadaResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-02",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-10",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 120m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta pendente",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 120m }
            }
        });

        await client.PostAsJsonAsync($"/api/v1/contas-pagar/{liquidada!.Id}/liquidar", new
        {
            dataLiquidacao = "2026-04-07",
            contaBancariaId = fixture.ContaBancariaId
        });

        var listResponse =
            await client.GetFromJsonAsync<ContaListResponse>("/api/v1/contas-pagar?statusCodigo=PENDENTE,LIQUIDADA");

        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().HaveCount(2);
        listResponse.Summary.TotalRegistros.Should().Be(2);
        listResponse.Summary.ValorTotal.Should().Be(201m);
    }

    [Fact]
    public async Task Get_NaoDeveListarComprasDeCartaoComoContasAPagarOperacionais()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoCartaoId,
            cartaoId = fixture.CartaoId,
            valorOriginal = 300m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 2,
            descricao = "Compra no cartao",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 300m }
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();
        var listResponse = await client.GetFromJsonAsync<ContaListResponse>("/api/v1/contas-pagar?search=Compra no cartao");

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Should().NotBeNull();
        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().BeEmpty();
        listResponse.Summary.TotalRegistros.Should().Be(0);
        listResponse.Summary.ValorTotal.Should().Be(0m);
    }

    [Fact]
    public async Task Post_CompraNoCartao_DeveRetornarCompetenciaPrevistaDaFatura()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-11",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoCartaoId,
            cartaoId = fixture.CartaoId,
            valorOriginal = 9150m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 10,
            descricao = "Tratamento dentista",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 9150m }
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Should().NotBeNull();
        created!.CartaoId.Should().Be(fixture.CartaoId);
        created.CompetenciaFaturaCartao.Should().Be("2026-05");
        created.DataFechamentoFaturaCartao.Should().Be(new DateOnly(2026, 5, 10));
        created.DataVencimentoFaturaCartao.Should().Be(new DateOnly(2026, 5, 20));
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
    public async Task Post_QuandoRateioUsarContaGerencialPai_DeveRetornarBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var contaPaiId = await CriarContaGerencialAsync(client, "DESP.PAI", "Despesas estruturais", "Despesa", null);
        _ = await CriarContaGerencialAsync(client, "DESP.FILHA", "Despesa filha", "Despesa", contaPaiId);

        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 100m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Despesa com conta pai",
            rateios = new[]
            {
                new { contaGerencialId = contaPaiId, valor = 100m }
            }
        });

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Errors.Should().ContainKey("Rateios");
    }

    [Fact]
    public async Task Post_QuandoRateioUsarContaGerencialDeReceita_DeveRetornarBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 100m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Despesa com conta de receita",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 100m }
            }
        });

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Errors.Should().ContainKey("Rateios");
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
                tipoDia = "DiaFixo",
                diaOrdemMensal = 20,
                dataInicio = (string?)null,
                dataFim = "2026-08-01",
                permiteEdicaoOcorrenciaIndividual = true,
                observacao = "Contrato mensal"
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        var gerarResponse = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{created!.Id}/gerar-ocorrencias", new
        {
            ateData = "2026-06-30"
        });

        var listResponse = await client.GetFromJsonAsync<ContaListResponse>("/api/v1/contas-pagar?search=Internet");

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Should().NotBeNull();
        created!.Recorrencia.Should().NotBeNull();
        created.Recorrencia!.TipoPeriodicidade.Should().Be("Mensal");
        created.Recorrencia.DataInicio.Should().Be(new DateOnly(2026, 5, 20));
        created.Recorrencia.DataFim.Should().Be(new DateOnly(2026, 8, 20));
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
                tipoDia = "DiaFixo",
                diaOrdemMensal = 20,
                dataInicio = (string?)null,
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

        var listResponse = await client.GetFromJsonAsync<ContaListResponse>("/api/v1/contas-pagar?search=Licenca");
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
                tipoDia = "DiaFixo",
                diaOrdemMensal = 20,
                dataInicio = (string?)null,
                dataFim = (string?)null,
                permiteEdicaoOcorrenciaIndividual = true,
                observacao = "Regra ajustada"
            }
        });

        alterarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var abril = await client.GetFromJsonAsync<ContaDetalheResponse>($"/api/v1/contas-pagar/{created.Id}");
        var junhoAtualizado = await client.GetFromJsonAsync<ContaDetalheResponse>($"/api/v1/contas-pagar/{junho.Id}");
        var julho = (await client.GetFromJsonAsync<ContaListResponse>("/api/v1/contas-pagar?search=Licenca"))!
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
                tipoDia = "DiaFixo",
                diaOrdemMensal = 15,
                dataInicio = (string?)null,
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

    private static async Task<Guid> CriarContaGerencialAsync(
        HttpClient client,
        string codigo,
        string descricao,
        string tipo,
        Guid? contaPaiId)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo,
            descricao,
            tipo,
            contaPaiId,
            ativo = true
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

    private sealed record ContaListSummaryResponse(
        int TotalRegistros,
        decimal ValorTotal,
        decimal TotalPendente,
        decimal TotalVencendoHoje,
        decimal TotalLiquidado);

    private sealed record ContaListResponse(
        IReadOnlyCollection<ContaResumoResponse> Items,
        int Page,
        int PageSize,
        int TotalItems,
        int TotalPages,
        ContaListSummaryResponse Summary);

    private sealed record ApiErrorResponse(string Code, string Message, IReadOnlyDictionary<string, string[]> Errors, string TraceId);

    private sealed record IdResponse(Guid Id);

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
        RecorrenciaResponse? Recorrencia,
        Guid? CartaoId,
        string? CompetenciaFaturaCartao,
        DateOnly? DataFechamentoFaturaCartao,
        DateOnly? DataVencimentoFaturaCartao);

    private sealed record RecorrenciaResponse(
        Guid Id,
        string TipoPeriodicidade,
        string TipoDia,
        int DiaOrdemMensal,
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


