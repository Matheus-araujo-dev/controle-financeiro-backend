using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class ContasReceberControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Post_QuandoRateioUsarContaGerencialPai_DeveRetornarBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var contaPaiId = await CriarContaGerencialAsync(client, "REC.PAI", "Receitas estruturais", "Receita", null);
        _ = await CriarContaGerencialAsync(client, "REC.FILHA", "Receita filha", "Receita", contaPaiId);

        var response = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-04",
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 200m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Receita com conta pai",
            rateios = new[]
            {
                new { contaGerencialId = contaPaiId, valor = 200m }
            }
        });

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Errors.Should().ContainKey("Rateios");
    }

    [Fact]
    public async Task Post_QuandoRateioUsarContaGerencialDeDespesa_DeveRetornarBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-04",
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 200m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Receita com conta de despesa",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 200m }
            }
        });

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Errors.Should().ContainKey("Rateios");
    }

    [Fact]
    public async Task Get_ComFiltroDeStatusDeveRetornarSummaryFiltrado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        var liquidadaResponse = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-04",
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-07",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 81m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Receita liquidada",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 81m }
            }
        });

        var liquidada = await liquidadaResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-04",
            pagadorId = fixture.PagadorId,
            dataVencimento = hoje.ToString("yyyy-MM-dd"),
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 200m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Receita pendente",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 200m }
            }
        });

        var liquidarResponse = await client.PostAsJsonAsync($"/api/v1/contas-receber/{liquidada!.Id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId,
            valorLiquidacao = 81m,
            atualizarValorConta = true
        });

        var listResponse = await client.GetFromJsonAsync<ContaListResponse>("/api/v1/contas-receber?statusCodigo=LIQUIDADA");

        liquidarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().ContainSingle(item => item.Descricao == "Receita liquidada");
        listResponse.Summary.TotalRegistros.Should().Be(1);
        listResponse.Summary.ValorTotal.Should().Be(81m);
        listResponse.Summary.TotalPendente.Should().Be(200m);
        listResponse.Summary.TotalVencendoHoje.Should().Be(200m);
        listResponse.Summary.TotalLiquidado.Should().Be(81m);
    }

    [Fact]
    public async Task Get_ComFiltrosDeDocumentoEmissaoValorERecorrencia_DeveFiltrarConta()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            numeroDocumento = "DOC-REC-001",
            dataEmissao = "2026-04-04",
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 150m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Receita recorrente filtrada",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 150m }
            },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                tipoDia = "DiaFixo",
                diaOrdemMensal = 25,
                dataInicio = (string?)null,
                dataFim = (string?)null,
                permiteEdicaoOcorrenciaIndividual = true,
                observacao = "Filtro recorrente"
            }
        });

        await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            numeroDocumento = "DOC-NORMAL-002",
            dataEmissao = "2026-04-04",
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 80m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Receita nao recorrente",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 80m }
            }
        });

        var listResponse = await client.GetFromJsonAsync<ContaListResponse>(
            "/api/v1/contas-receber?numeroDocumento=DOC-REC&dataEmissaoInicial=2026-04-01&dataEmissaoFinal=2026-04-30&valorMinimo=140&valorMaximo=160&ehRecorrente=true");

        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().ContainSingle(item => item.NumeroDocumento == "DOC-REC-001");
        listResponse.Items.Should().OnlyContain(item => item.EhRecorrente);
        listResponse.Summary.TotalRegistros.Should().Be(1);
    }

    [Fact]
    public async Task Get_ComMultiplosStatusDeveRetornarSummaryFiltrado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var liquidadaResponse = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-04",
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-07",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 81m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Receita liquidada",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 81m }
            }
        });

        var liquidada = await liquidadaResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-04",
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 200m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Receita pendente",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 200m }
            }
        });

        await client.PostAsJsonAsync($"/api/v1/contas-receber/{liquidada!.Id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId,
            valorLiquidacao = 81m,
            atualizarValorConta = true
        });

        var listResponse =
            await client.GetFromJsonAsync<ContaListResponse>("/api/v1/contas-receber?statusCodigo=PENDENTE,LIQUIDADA");

        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().HaveCount(2);
        listResponse.Summary.TotalRegistros.Should().Be(2);
        listResponse.Summary.ValorTotal.Should().Be(281m);
    }

    [Fact]
    public async Task PostLiquidar_DeveGerarMovimentacaoDeEntrada()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-04",
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 200m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Receita principal",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 200m }
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        var liquidarResponse = await client.PostAsJsonAsync($"/api/v1/contas-receber/{created!.Id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId,
            valorLiquidacao = 200m,
            atualizarValorConta = true
        });

        var movimento = await client.GetFromJsonAsync<PagedResponse<MovimentacaoResumoResponse>>("/api/v1/movimentacoes?search=Receita principal");

        liquidarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        movimento.Should().NotBeNull();
        movimento!.Items.Should().ContainSingle(item =>
            item.Tipo == "Entrada" &&
            item.Natureza == "Realizada" &&
            item.ContaReceberId == created.Id &&
            item.Valor == 200m);
    }

    [Fact]
    public async Task PostLiquidar_QuandoValorForMenorEUsuarioNaoAtualizar_DeveMarcarParcial()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-04",
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 180m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Receita parcial",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 180m }
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        var liquidarResponse = await client.PostAsJsonAsync($"/api/v1/contas-receber/{created!.Id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId,
            valorLiquidacao = 100m,
            atualizarValorConta = false
        });

        var movimento = await client.GetFromJsonAsync<PagedResponse<MovimentacaoResumoResponse>>("/api/v1/movimentacoes?search=Receita parcial");
        var contaAtualizada = await liquidarResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();
        var liquidacaoJson = JsonDocument.Parse(await liquidarResponse.Content.ReadAsStringAsync());

        liquidarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        contaAtualizada.Should().NotBeNull();
        liquidacaoJson.RootElement.GetProperty("statusCodigo").GetString().Should().Be("PARCIAL");
        contaAtualizada!.ValorLiquido.Should().Be(180m);
        movimento.Should().NotBeNull();
        movimento!.Items.Should().ContainSingle(item =>
            item.Tipo == "Entrada" &&
            item.Natureza == "Realizada" &&
            item.ContaReceberId == created.Id &&
            item.Valor == 100m);
    }

    [Fact]
    public async Task Post_ComRecorrenciaDeveGerarOcorrenciasFuturasDeContasReceber()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-04",
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 300m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Mensalidade consultoria",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 300m }
            },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                tipoDia = "DiaFixo",
                diaOrdemMensal = 25,
                dataInicio = (string?)null,
                dataFim = "2026-08-01",
                permiteEdicaoOcorrenciaIndividual = true
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        var gerarResponse = await client.PostAsJsonAsync($"/api/v1/contas-receber/{created!.Id}/gerar-ocorrencias", new
        {
            ateData = "2026-06-30"
        });

        var listResponse = await client.GetFromJsonAsync<ContaListResponse>("/api/v1/contas-receber?search=Mensalidade");

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Should().NotBeNull();
        created!.Recorrencia.Should().NotBeNull();
        created.Recorrencia!.DataInicio.Should().Be(new DateOnly(2026, 5, 25));
        created.Recorrencia.DataFim.Should().Be(new DateOnly(2026, 8, 25));
        gerarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().HaveCount(3);
        listResponse.Items.Should().OnlyContain(item => item.EhRecorrente);
    }

    [Fact]
    public async Task Put_QuandoContaPendenteRecebeRecorrencia_DeveVincularRegraEPermitirGerarOcorrencias()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-04",
            responsavelId = fixture.ResponsavelId,
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 300m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Mensalidade editada",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 300m }
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/contas-receber/{created!.Id}", new
        {
            id = created.Id,
            dataEmissao = "2026-04-04",
            responsavelId = fixture.ResponsavelId,
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 300m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Mensalidade editada",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 300m }
            },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                tipoDia = "DiaFixo",
                diaOrdemMensal = 25,
                dataInicio = (string?)null,
                dataFim = "2026-06-01",
                permiteEdicaoOcorrenciaIndividual = true,
                observacao = "Ativada na edicao"
            }
        });

        var updated = await updateResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();
        var reloaded = await client.GetFromJsonAsync<ContaDetalheResponse>($"/api/v1/contas-receber/{created.Id}");
        var gerarResponse = await client.PostAsJsonAsync($"/api/v1/contas-receber/{created.Id}/gerar-ocorrencias", new
        {
            ateData = "2026-06-30"
        });
        var listResponse = await client.GetFromJsonAsync<ContaListResponse>("/api/v1/contas-receber?search=Mensalidade");

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        updated!.EhRecorrente.Should().BeTrue();
        updated.Recorrencia.Should().NotBeNull();
        updated.Recorrencia!.DataInicio.Should().Be(new DateOnly(2026, 5, 25));
        updated.Recorrencia.DataFim.Should().Be(new DateOnly(2026, 6, 25));
        reloaded!.EhRecorrente.Should().BeTrue();
        reloaded.Recorrencia.Should().NotBeNull();
        gerarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        listResponse!.Items.Should().HaveCount(3);
        listResponse.Items.Should().OnlyContain(item => item.EhRecorrente);
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

    private sealed record ContaDetalheResponse(
        Guid Id,
        string Descricao,
        decimal ValorLiquido,
        int QuantidadeParcelas,
        int NumeroParcela,
        bool EhRecorrente,
        RecorrenciaResponse? Recorrencia);

    private sealed record ContaResumoResponse(Guid Id, string? NumeroDocumento, string Descricao, decimal ValorLiquido, bool EhRecorrente);

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
