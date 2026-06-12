using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.PlanejamentoCompras;

public sealed class ComprasPlanejadasControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task PostEGet_DeveCriarCompraPlanejadaComContaGerencialEResponsavel()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var responsavelId = await CriarPessoaAsync(client, "Michelle");
        var contaGerencialId = await CriarContaGerencialAsync(client, "TEC", "Tecnologia");

        var createResponse = await client.PostAsJsonAsync("/api/v1/compras-planejadas", new
        {
            titulo = "Notebook novo",
            descricao = "Troca do equipamento principal",
            valorEstimado = 4500m,
            dataDesejada = "2026-11-20",
            prioridade = "Alta",
            status = "Planejada",
            parcelavel = true,
            quantidadeParcelasDesejada = 10,
            contaGerencialId,
            responsavelId,
            link = "https://loja.exemplo.com/produto/notebook",
            observacao = "Aguardar Black Friday"
        });

        var created = await createResponse.Content.ReadFromJsonAsync<CompraPlanejadaDetalheResponse>();
        var detail = await client.GetFromJsonAsync<CompraPlanejadaDetalheResponse>($"/api/v1/compras-planejadas/{created!.Id}");

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        detail.Should().NotBeNull();
        detail!.Titulo.Should().Be("Notebook novo");
        detail.ContaGerencialId.Should().Be(contaGerencialId);
        detail.ContaGerencialDescricao.Should().Be("Tecnologia");
        detail.ResponsavelId.Should().Be(responsavelId);
        detail.ResponsavelNome.Should().Be("Michelle");
        detail.Link.Should().Be("https://loja.exemplo.com/produto/notebook");
        detail.QuantidadeParcelasDesejada.Should().Be(10);
    }

    [Fact]
    public async Task Post_QuandoQuantidadeParcelasInvalida_DeveRetornarErroDeValidacao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var responsavelId = await CriarPessoaAsync(client, "Michelle");
        var contaGerencialId = await CriarContaGerencialAsync(client, "TEC", "Tecnologia");

        var response = await client.PostAsJsonAsync("/api/v1/compras-planejadas", new
        {
            titulo = "Notebook novo",
            valorEstimado = 4500m,
            prioridade = "Alta",
            status = "Planejada",
            parcelavel = true,
            quantidadeParcelasDesejada = 1,
            contaGerencialId,
            responsavelId
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_QuandoContaGerencialForPai_DeveRetornarErroDeValidacao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var responsavelId = await CriarPessoaAsync(client, "Michelle");
        var contaPaiId = await CriarContaGerencialAsync(client, "DESP", "Despesas planejadas", null);
        _ = await CriarContaGerencialAsync(client, "DESP.TEC", "Tecnologia", contaPaiId);

        var response = await client.PostAsJsonAsync("/api/v1/compras-planejadas", new
        {
            titulo = "Notebook novo",
            valorEstimado = 4500m,
            prioridade = "Alta",
            status = "Planejada",
            parcelavel = true,
            quantidadeParcelasDesejada = 10,
            contaGerencialId = contaPaiId,
            responsavelId
        });

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Errors.Should().ContainKey("ContaGerencialId");
    }

    [Fact]
    public async Task Post_QuandoContaGerencialForReceita_DeveRetornarErroDeValidacao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var responsavelId = await CriarPessoaAsync(client, "Michelle");
        var contaGerencialReceitaId = await CriarContaGerencialAsync(client, "REC", "Receitas diversas", null, "Receita");

        var response = await client.PostAsJsonAsync("/api/v1/compras-planejadas", new
        {
            titulo = "Notebook novo",
            valorEstimado = 4500m,
            prioridade = "Alta",
            status = "Planejada",
            parcelavel = true,
            quantidadeParcelasDesejada = 10,
            contaGerencialId = contaGerencialReceitaId,
            responsavelId
        });

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Errors.Should().ContainKey("ContaGerencialId");
    }

    [Fact]
    public async Task Get_ComFiltroDeStatusDeveRetornarSummaryFiltrado()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var responsavelId = await CriarPessoaAsync(client, "Michelle");
        var contaGerencialId = await CriarContaGerencialAsync(client, "TEC", "Tecnologia");

        await client.PostAsJsonAsync("/api/v1/compras-planejadas", new
        {
            titulo = "Notebook novo",
            valorEstimado = 4500m,
            prioridade = "Alta",
            status = "Planejada",
            parcelavel = true,
            quantidadeParcelasDesejada = 10,
            contaGerencialId,
            responsavelId
        });

        await client.PostAsJsonAsync("/api/v1/compras-planejadas", new
        {
            titulo = "Mesa cancelada",
            valorEstimado = 1200m,
            prioridade = "Baixa",
            status = "Cancelada",
            parcelavel = false,
            quantidadeParcelasDesejada = (int?)null,
            contaGerencialId,
            responsavelId
        });

        var listResponse = await client.GetFromJsonAsync<CompraPlanejadaListResponse>("/api/v1/compras-planejadas?status=Planejada");

        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().ContainSingle(item => item.Titulo == "Notebook novo");
        listResponse.Summary.TotalRegistros.Should().Be(1);
        listResponse.Summary.ValorTotalEstimado.Should().Be(4500m);
    }

    [Fact]
    public async Task TransformarEmContaPagar_DeveVincularCompraPlanejadaEAtualizarStatus()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var compraPlanejadaId = await CriarCompraPlanejadaAsync(client, fixture);

        var criarContaPagarResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            origemCompraPlanejadaId = compraPlanejadaId,
            dataEmissao = "2026-05-20",
            responsavelCompraId = fixture.ResponsavelId,
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-05-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 4500m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Notebook novo",
            observacao = "Conversao do planejador",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 4500m }
            }
        });

        criarContaPagarResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var contaCriada = await criarContaPagarResponse.Content.ReadFromJsonAsync<IdResponse>();

        var detalhe = await client.GetFromJsonAsync<CompraPlanejadaDetalheResponse>($"/api/v1/compras-planejadas/{compraPlanejadaId}");

        detalhe.Should().NotBeNull();
        detalhe!.Status.Should().Be("Comprada");
        detalhe.ContaPagarGeradaId.Should().Be(contaCriada!.Id);
        detalhe.ConvertidaEmContaPagarEmUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task TransformarEmContaPagar_QuandoCompraJaEstiverConvertida_DeveRetornarBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var compraPlanejadaId = await CriarCompraPlanejadaAsync(client, fixture);

        var payload = new
        {
            origemCompraPlanejadaId = compraPlanejadaId,
            dataEmissao = "2026-05-20",
            responsavelCompraId = fixture.ResponsavelId,
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-05-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 4500m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Notebook novo",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 4500m }
            }
        };

        (await client.PostAsJsonAsync("/api/v1/contas-pagar", payload)).StatusCode.Should().Be(HttpStatusCode.Created);

        var segundaTentativa = await client.PostAsJsonAsync("/api/v1/contas-pagar", payload);

        segundaTentativa.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_QuandoCompraJaEstiverRealizada_DeveRetornarBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var compraPlanejadaId = await CriarCompraPlanejadaAsync(client, fixture);

        var realizarResponse = await client.PostAsJsonAsync($"/api/v1/compras-planejadas/{compraPlanejadaId}/realizar", new
        {
            dataCompra = "2026-05-02",
            recebedorId = fixture.RecebedorId,
            formaPagamentoId = fixture.FormaPagamentoAutoId,
            contaBancariaId = fixture.ContaBancariaId,
            quantidadeParcelas = 1
        });

        realizarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var atualizarResponse = await client.PutAsJsonAsync($"/api/v1/compras-planejadas/{compraPlanejadaId}", new
        {
            titulo = "Notebook ajustado",
            descricao = "Nao deveria aceitar alteracao",
            valorEstimado = 4600m,
            dataDesejada = "2026-05-25",
            prioridade = "Alta",
            status = "Comprada",
            parcelavel = true,
            quantidadeParcelasDesejada = 10,
            contaGerencialId = fixture.ContaGerencialDespesaId,
            responsavelId = fixture.ResponsavelId,
            link = "https://loja.exemplo.com/produto/notebook",
            observacao = "Alteracao indevida"
        });

        var error = await atualizarResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();

        atualizarResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Errors.Should().ContainKey("Status");
    }

    [Fact]
    public async Task PostRealizar_QuandoFormaBaixaAutomaticamente_DeveCriarContaLiquidadaEGerarMovimentacao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var compraPlanejadaId = await CriarCompraPlanejadaAsync(client, fixture);

        var realizarResponse = await client.PostAsJsonAsync($"/api/v1/compras-planejadas/{compraPlanejadaId}/realizar", new
        {
            dataCompra = "2026-05-02",
            recebedorId = fixture.RecebedorId,
            formaPagamentoId = fixture.FormaPagamentoAutoId,
            contaBancariaId = fixture.ContaBancariaId,
            quantidadeParcelas = 1
        });

        var detalhe = await client.GetFromJsonAsync<CompraPlanejadaDetalheResponse>($"/api/v1/compras-planejadas/{compraPlanejadaId}");
        var conta = await client.GetFromJsonAsync<ContaPagarDetalheResponse>($"/api/v1/contas-pagar/{detalhe!.ContaPagarGeradaId}");
        var movimentos = await client.GetFromJsonAsync<PagedResponse<MovimentacaoResumoResponse>>("/api/v1/movimentacoes?search=Notebook");

        realizarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        detalhe.Should().NotBeNull();
        detalhe!.Status.Should().Be("Comprada");
        detalhe.ContaPagarGeradaId.Should().NotBeNull();
        conta.Should().NotBeNull();
        conta!.StatusCodigo.Should().Be("LIQUIDADA");
        conta.DataLiquidacao.Should().Be(new DateOnly(2026, 5, 2));

        movimentos.Should().NotBeNull();
        movimentos!.Items.Should().ContainSingle(item =>
            item.ContaPagarId == detalhe.ContaPagarGeradaId &&
            item.Natureza == "Realizada" &&
            item.ContaBancariaId == fixture.ContaBancariaId &&
            item.Valor == 4500m);
    }

    [Fact]
    public async Task PostRealizar_QuandoFormaNaoBaixaAutomaticamente_DeveGerarContaPagarPendente()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var compraPlanejadaId = await CriarCompraPlanejadaAsync(client, fixture);

        var realizarResponse = await client.PostAsJsonAsync($"/api/v1/compras-planejadas/{compraPlanejadaId}/realizar", new
        {
            dataCompra = "2026-05-02",
            dataVencimento = "2026-05-25",
            recebedorId = fixture.RecebedorId,
            formaPagamentoId = fixture.FormaPagamentoManualId,
            quantidadeParcelas = 1
        });

        var detalhe = await client.GetFromJsonAsync<CompraPlanejadaDetalheResponse>($"/api/v1/compras-planejadas/{compraPlanejadaId}");
        var conta = await client.GetFromJsonAsync<ContaPagarDetalheResponse>($"/api/v1/contas-pagar/{detalhe!.ContaPagarGeradaId}");
        var movimentos = await client.GetFromJsonAsync<PagedResponse<MovimentacaoResumoResponse>>("/api/v1/movimentacoes?search=Notebook");

        realizarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        detalhe.Should().NotBeNull();
        detalhe!.Status.Should().Be("Comprada");
        conta.Should().NotBeNull();
        conta!.StatusCodigo.Should().Be("PENDENTE");
        conta.DataLiquidacao.Should().BeNull();
        conta.DataVencimento.Should().Be(new DateOnly(2026, 5, 25));

        movimentos.Should().NotBeNull();
        movimentos!.Items.Should().NotContain(item => item.ContaPagarId == detalhe.ContaPagarGeradaId);
    }

    [Fact]
    public async Task PostRealizar_QuandoFormaForCartaoParcelado_DevePlanejarParcelasPorCompetenciaDeFaturaSemGerarMovimentacoes()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var compraPlanejadaId = await CriarCompraPlanejadaAsync(client, fixture);

        var realizarResponse = await client.PostAsJsonAsync($"/api/v1/compras-planejadas/{compraPlanejadaId}/realizar", new
        {
            dataCompra = "2026-05-12",
            recebedorId = fixture.RecebedorId,
            formaPagamentoId = fixture.FormaPagamentoCartaoId,
            cartaoId = fixture.CartaoId,
            quantidadeParcelas = 3
        });

        var detalhe = await client.GetFromJsonAsync<CompraPlanejadaDetalheResponse>($"/api/v1/compras-planejadas/{compraPlanejadaId}");
        var contas = await client.GetFromJsonAsync<ContaListResponse>("/api/v1/contas-pagar?search=Notebook");
        var faturas = await client.GetFromJsonAsync<PagedResponse<FaturaResumoResponse>>("/api/v1/faturas");
        var movimentos = await client.GetFromJsonAsync<PagedResponse<MovimentacaoResumoResponse>>("/api/v1/movimentacoes?search=Notebook");

        realizarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        detalhe.Should().NotBeNull();
        detalhe!.Status.Should().Be("Comprada");
        detalhe.ContaPagarGeradaId.Should().BeNull();

        contas.Should().NotBeNull();
        contas!.Items.Should().BeEmpty();

        faturas.Should().NotBeNull();
        faturas!.Items.Should().Contain(item => item.Competencia == "2026-06" && item.ValorTotal == 1500m);
        faturas.Items.Should().Contain(item => item.Competencia == "2026-07" && item.ValorTotal == 1500m);
        faturas.Items.Should().Contain(item => item.Competencia == "2026-08" && item.ValorTotal == 1500m);

        movimentos.Should().NotBeNull();
        movimentos!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task PostRealizar_QuandoFormaForCartaoComBaixaAutomatica_DeveAceitarEPlanejarParcelas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var compraPlanejadaId = await CriarCompraPlanejadaAsync(client, fixture);
        var formaPagamentoCartaoAutoId = await CriarFormaPagamentoAsync(client, "Cartao auto", "Credito", true, true);

        var realizarResponse = await client.PostAsJsonAsync($"/api/v1/compras-planejadas/{compraPlanejadaId}/realizar", new
        {
            dataCompra = "2026-05-12",
            recebedorId = fixture.RecebedorId,
            formaPagamentoId = formaPagamentoCartaoAutoId,
            cartaoId = fixture.CartaoId,
            quantidadeParcelas = 2
        });

        var detalhe = await client.GetFromJsonAsync<CompraPlanejadaDetalheResponse>($"/api/v1/compras-planejadas/{compraPlanejadaId}");
        var contas = await client.GetFromJsonAsync<ContaListResponse>("/api/v1/contas-pagar?search=Notebook");

        realizarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        detalhe.Should().NotBeNull();
        detalhe!.Status.Should().Be("Comprada");
        detalhe.ContaPagarGeradaId.Should().BeNull();
        contas.Should().NotBeNull();
        contas!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetList_AposRealizarCompraNoCartao_DeveRetornarItemCompradoSemContaPagarGerada()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var compraPlanejadaId = await CriarCompraPlanejadaAsync(client, fixture);

        var realizarResponse = await client.PostAsJsonAsync($"/api/v1/compras-planejadas/{compraPlanejadaId}/realizar", new
        {
            dataCompra = "2026-05-12",
            recebedorId = fixture.RecebedorId,
            formaPagamentoId = fixture.FormaPagamentoCartaoId,
            cartaoId = fixture.CartaoId,
            quantidadeParcelas = 3
        });

        var listResponse = await client.GetFromJsonAsync<CompraPlanejadaListResponse>("/api/v1/compras-planejadas?status=Comprada");

        realizarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().ContainSingle(item =>
            item.Id == compraPlanejadaId &&
            item.Status == "Comprada" &&
            item.ContaPagarGeradaId == null);
        listResponse.Summary.TotalRegistros.Should().Be(1);
        listResponse.Summary.ValorTotalEstimado.Should().Be(4500m);
    }

    private static async Task<Guid> CriarCompraPlanejadaAsync(HttpClient client, FinancialFixtureSeed.FixtureIds fixture)
    {
        var response = await client.PostAsJsonAsync("/api/v1/compras-planejadas", new
        {
            titulo = "Notebook novo",
            descricao = "Troca do equipamento principal",
            valorEstimado = 4500m,
            dataDesejada = "2026-05-25",
            prioridade = "Alta",
            status = "Planejada",
            parcelavel = true,
            quantidadeParcelasDesejada = 10,
            contaGerencialId = fixture.ContaGerencialDespesaId,
            responsavelId = fixture.ResponsavelId,
            link = "https://loja.exemplo.com/produto/notebook",
            observacao = "Aguardar melhor janela"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private static async Task<Guid> CriarPessoaAsync(HttpClient client, string nome)
    {
        var response = await client.PostAsJsonAsync("/api/v1/pessoas", new
        {
            nome,
            tipoPessoa = "Fisica"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private static async Task<Guid> CriarContaGerencialAsync(
        HttpClient client,
        string codigo,
        string descricao,
        Guid? contaPaiId = null,
        string tipo = "Despesa")
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

    private static async Task<Guid> CriarFormaPagamentoAsync(
        HttpClient client,
        string nome,
        string tipo,
        bool ehCartao,
        bool baixarAutomaticamente)
    {
        var response = await client.PostAsJsonAsync("/api/v1/formas-pagamento", new
        {
            nome,
            tipo,
            ehCartao,
            baixarAutomaticamente,
            ativo = true
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record ApiErrorResponse(string Code, string Message, IReadOnlyDictionary<string, string[]> Errors, string TraceId);

    private sealed record CompraPlanejadaDetalheResponse(
        Guid Id,
        string Titulo,
        string? Descricao,
        decimal ValorEstimado,
        DateOnly? DataDesejada,
        string Prioridade,
        string Status,
        bool Parcelavel,
        int? QuantidadeParcelasDesejada,
        Guid ContaGerencialId,
        string ContaGerencialDescricao,
        Guid ResponsavelId,
        string ResponsavelNome,
        string? Link,
        string? Observacao,
        Guid? ContaPagarGeradaId,
        DateTime? ConvertidaEmContaPagarEmUtc,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);

    private sealed record CompraPlanejadaListSummaryResponse(int TotalRegistros, decimal ValorTotalEstimado);

    private sealed record CompraPlanejadaListResponse(
        IReadOnlyCollection<CompraPlanejadaResumoResponse> Items,
        int Page,
        int PageSize,
        int TotalItems,
        int TotalPages,
        CompraPlanejadaListSummaryResponse Summary);

    private sealed record CompraPlanejadaResumoResponse(
        Guid Id,
        string Titulo,
        decimal ValorEstimado,
        DateOnly? DataDesejada,
        string Prioridade,
        string Status,
        bool Parcelavel,
        int? QuantidadeParcelasDesejada,
        Guid ContaGerencialId,
        string ContaGerencialDescricao,
        Guid ResponsavelId,
        string ResponsavelNome,
        string? Link,
        Guid? ContaPagarGeradaId,
        DateTime? ConvertidaEmContaPagarEmUtc);

    private sealed record ContaPagarDetalheResponse(
        Guid Id,
        string StatusCodigo,
        DateOnly DataVencimento,
        DateOnly? DataLiquidacao);

    private sealed record ContaListResponse(
        IReadOnlyCollection<ContaPagarResumoResponse> Items,
        int Page,
        int PageSize,
        int TotalItems,
        int TotalPages);

    private sealed record ContaPagarResumoResponse(
        Guid Id,
        string Descricao,
        decimal ValorLiquido,
        int QuantidadeParcelas,
        int NumeroParcela,
        DateOnly DataVencimento);

    private sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

    private sealed record MovimentacaoResumoResponse(
        Guid Id,
        DateOnly DataMovimentacao,
        string Tipo,
        string Natureza,
        string StatusCodigo,
        decimal Valor,
        Guid? ContaBancariaId,
        Guid? ContaPagarId,
        Guid? ContaReceberId,
        Guid? FaturaCartaoId,
        string? Observacao);

    private sealed record FaturaResumoResponse(
        Guid Id,
        Guid CartaoId,
        string CartaoNome,
        string Competencia,
        DateOnly DataFechamento,
        DateOnly DataVencimento,
        decimal ValorTotal,
        DateOnly? DataPagamento,
        string StatusCodigo,
        string StatusNome,
        int QuantidadeItens);
}
