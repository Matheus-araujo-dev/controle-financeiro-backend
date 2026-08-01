using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class ContasPagarFluxoTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private sealed record ContaResumo(Guid Id, string StatusCodigo);

    private static async Task<Guid> CriarContaPagarAsync(HttpClient client, FinancialFixtureSeed.FixtureIds fixture, decimal valor = 150m)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-25",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = valor,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Despesa fluxo",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor } }
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
        var id = await CriarContaPagarAsync(client, fixture);

        var liquidar = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId,
            valorLiquidacao = 150m,
            atualizarValorConta = true
        });
        liquidar.StatusCode.Should().Be(HttpStatusCode.OK);
        (await liquidar.Content.ReadFromJsonAsync<ContaResumo>())!.StatusCodigo.Should().Be("LIQUIDADA");

        var estornar = await client.PostAsync($"/api/v1/contas-pagar/{id}/estornar", content: null);
        estornar.StatusCode.Should().Be(HttpStatusCode.OK);
        (await estornar.Content.ReadFromJsonAsync<ContaResumo>())!.StatusCodigo.Should().Be("PENDENTE");
    }

    [Fact]
    public async Task Cancelar_DeveMarcarComoCancelada()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarContaPagarAsync(client, fixture);

        var cancelar = await client.PostAsync($"/api/v1/contas-pagar/{id}/cancelar", content: null);

        cancelar.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cancelar.Content.ReadFromJsonAsync<ContaResumo>())!.StatusCodigo.Should().Be("CANCELADA");
    }

    [Fact]
    public async Task ObterPorId_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resposta = await client.GetAsync($"/api/v1/contas-pagar/{Guid.NewGuid()}");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Estornar_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resposta = await client.PostAsync($"/api/v1/contas-pagar/{Guid.NewGuid()}/estornar", content: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LiquidarParcial_DeveSetarStatusParcialEExporValorPago()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarContaPagarAsync(client, fixture, valor: 100m);

        var liquidar = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId,
            valorLiquidacao = 60m,
            atualizarValorConta = false,
            cancelarValorRestante = false
        });

        liquidar.StatusCode.Should().Be(HttpStatusCode.OK);
        var detalhe = await liquidar.Content.ReadFromJsonAsync<ContaDetalhe>();
        detalhe!.StatusCodigo.Should().Be("PARCIAL");
        detalhe.ValorPago.Should().Be(60m);
        detalhe.ValorLiquido.Should().Be(100m);
    }

    [Fact]
    public async Task LiquidarParcial_CancelarRestante_DeveSetarLiquidada()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarContaPagarAsync(client, fixture, valor: 100m);

        var liquidar = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId,
            valorLiquidacao = 60m,
            atualizarValorConta = false,
            cancelarValorRestante = true
        });

        liquidar.StatusCode.Should().Be(HttpStatusCode.OK);
        var detalhe = await liquidar.Content.ReadFromJsonAsync<ContaDetalhe>();
        detalhe!.StatusCodigo.Should().Be("LIQUIDADA");
        detalhe.ValorLiquido.Should().Be(60m);
    }

    [Fact]
    public async Task LiquidarParcial_EstornarDevolver_DeveVoltarParaPendente()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarContaPagarAsync(client, fixture, valor: 100m);

        await client.PostAsJsonAsync($"/api/v1/contas-pagar/{id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId,
            valorLiquidacao = 60m,
            atualizarValorConta = false,
            cancelarValorRestante = false
        });

        var estornar = await client.PostAsync($"/api/v1/contas-pagar/{id}/estornar", content: null);

        estornar.StatusCode.Should().Be(HttpStatusCode.OK);
        (await estornar.Content.ReadFromJsonAsync<ContaResumo>())!.StatusCodigo.Should().Be("PENDENTE");
    }

    [Fact]
    public async Task CancelarParcial_DeveTrimValueELiquidar()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarContaPagarAsync(client, fixture, valor: 100m);

        await client.PostAsJsonAsync($"/api/v1/contas-pagar/{id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId,
            valorLiquidacao = 60m,
            atualizarValorConta = false,
            cancelarValorRestante = false
        });

        var cancelar = await client.PostAsync($"/api/v1/contas-pagar/{id}/cancelar", content: null);

        cancelar.StatusCode.Should().Be(HttpStatusCode.OK);
        var detalhe = await cancelar.Content.ReadFromJsonAsync<ContaDetalhe>();
        detalhe!.StatusCodigo.Should().Be("LIQUIDADA");
        detalhe.ValorLiquido.Should().Be(60m);
    }

    private sealed record ContaDetalhe(Guid Id, string StatusCodigo, decimal ValorLiquido, decimal? ValorPago);

    // ── helpers para testes de fatura ────────────────────────────────────────

    private static async Task<Guid> CriarCompraCartaoAsync(HttpClient client, FinancialFixtureSeed.FixtureIds fixture, decimal valor = 200m)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-05",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoCartaoId,
            cartaoId = fixture.CartaoId,
            valorOriginal = valor,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Compra no cartao",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor } }
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ContaResumo>();
        return created!.Id;
    }

    // ── RemoverDaFatura ───────────────────────────────────────────────────────

    [Fact]
    public async Task RemoverDaFatura_ComContaEmFatura_DeveRetornar204EExcluirConta()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarCompraCartaoAsync(client, fixture);

        var detalhe = await client.GetFromJsonAsync<ContaResumo>($"/api/v1/contas-pagar/{id}");
        detalhe!.StatusCodigo.Should().Be("EM_FATURA");

        var remover = await client.DeleteAsync($"/api/v1/contas-pagar/{id}/remover-da-fatura");
        remover.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var aposRemocao = await client.GetAsync($"/api/v1/contas-pagar/{id}");
        aposRemocao.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoverDaFatura_ContaInexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var remover = await client.DeleteAsync($"/api/v1/contas-pagar/{Guid.NewGuid()}/remover-da-fatura");
        remover.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoverDaFatura_ContaPendente_DeveRetornar400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarContaPagarAsync(client, fixture);

        var detalhe = await client.GetFromJsonAsync<ContaResumo>($"/api/v1/contas-pagar/{id}");
        detalhe!.StatusCodigo.Should().Be("PENDENTE");

        var remover = await client.DeleteAsync($"/api/v1/contas-pagar/{id}/remover-da-fatura");
        remover.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemoverDaFatura_FaturaFechada_DeveRetornar400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarCompraCartaoAsync(client, fixture);

        // Fechar a fatura
        var faturaId = await ObterFaturaIdAsync(client, "2026-04");
        var fechar = await client.PostAsync($"/api/v1/faturas/{faturaId}/fechar", null);
        fechar.StatusCode.Should().Be(HttpStatusCode.OK);

        var remover = await client.DeleteAsync($"/api/v1/contas-pagar/{id}/remover-da-fatura");
        remover.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemoverDaFatura_ComReembolsoPendente_DeveExcluirContaEReembolso()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarCompraCartaoAsync(client, fixture);

        // Criar reembolso
        var reembolsoResp = await client.PostAsJsonAsync("/api/v1/reembolsos/contas-pagar", new
        {
            contaOrigemId = id,
            parcelarIgual = false,
            valorTotal = 200m,
            pagadoresIds = new[] { fixture.PagadorId },
            formaPagamentoId = fixture.FormaPagamentoManualId,
            dataVencimento = "2026-05-01",
            descricao = "Reembolso compra cartao",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 200m } }
        });
        reembolsoResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var reembolso = await reembolsoResp.Content.ReadFromJsonAsync<ReembolsoResponse>();
        var crId = reembolso!.ContasReceber[0].Id;

        // Remover da fatura deve funcionar e excluir o reembolso pendente também
        var remover = await client.DeleteAsync($"/api/v1/contas-pagar/{id}/remover-da-fatura");
        remover.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var contaRemovida = await client.GetAsync($"/api/v1/contas-pagar/{id}");
        contaRemovida.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var reembolsoRemovido = await client.GetAsync($"/api/v1/contas-receber/{crId}");
        reembolsoRemovido.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoverDaFatura_ComReembolsoJaRecebido_DeveRetornar400()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var id = await CriarCompraCartaoAsync(client, fixture);

        // Criar reembolso
        var reembolsoResp = await client.PostAsJsonAsync("/api/v1/reembolsos/contas-pagar", new
        {
            contaOrigemId = id,
            parcelarIgual = false,
            valorTotal = 200m,
            pagadoresIds = new[] { fixture.PagadorId },
            formaPagamentoId = fixture.FormaPagamentoManualId,
            dataVencimento = "2026-05-01",
            descricao = "Reembolso compra cartao",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 200m } }
        });
        var reembolso = await reembolsoResp.Content.ReadFromJsonAsync<ReembolsoResponse>();
        var crId = reembolso!.ContasReceber[0].Id;

        // Liquidar o reembolso (usar valorLiquidacao igual ao valor da conta)
        var liquidar = await client.PostAsJsonAsync($"/api/v1/contas-receber/{crId}/liquidar", new
        {
            dataLiquidacao = "2026-05-01",
            contaBancariaId = fixture.ContaBancariaId,
            valorLiquidacao = 200m,
            atualizarValorConta = true
        });
        liquidar.StatusCode.Should().Be(HttpStatusCode.OK);

        // Tentar remover da fatura deve falhar
        var remover = await client.DeleteAsync($"/api/v1/contas-pagar/{id}/remover-da-fatura");
        remover.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemoverDaFatura_Parcelada_DeveRemoverParcelasFuturas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        // Criar compra parcelada em 3x no cartão (parcelas em abril, maio, junho)
        var criarResp = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-05",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoCartaoId,
            cartaoId = fixture.CartaoId,
            valorOriginal = 300m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 3,
            descricao = "Notebook parcelado",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 300m } }
        });
        criarResp.EnsureSuccessStatusCode();
        var parcela1 = await criarResp.Content.ReadFromJsonAsync<ContaResumo>();
        parcela1!.StatusCodigo.Should().Be("EM_FATURA");

        // Verificar que cada mês tem 1 item na fatura antes da remoção
        var abrilId = await ObterFaturaIdAsync(client, "2026-04");
        var maioId = await ObterFaturaIdAsync(client, "2026-05");
        var junhoId = await ObterFaturaIdAsync(client, "2026-06");

        (await ObterTotalItensFaturaAsync(client, abrilId)).Should().Be(1);
        (await ObterTotalItensFaturaAsync(client, maioId)).Should().Be(1);
        (await ObterTotalItensFaturaAsync(client, junhoId)).Should().Be(1);

        // Obter IDs das parcelas futuras via fatura
        var parcelaMaioId = await ObterContaPagarIdDaFaturaAsync(client, maioId);
        var parcelaJunhoId = await ObterContaPagarIdDaFaturaAsync(client, junhoId);

        // Remover parcela 1 → deve remover também parcelas de maio e junho
        var remover = await client.DeleteAsync($"/api/v1/contas-pagar/{parcela1.Id}/remover-da-fatura");
        remover.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Todas as parcelas devem ter sido excluídas
        (await client.GetAsync($"/api/v1/contas-pagar/{parcela1.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/v1/contas-pagar/{parcelaMaioId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/v1/contas-pagar/{parcelaJunhoId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── helpers adicionais ───────────────────────────────────────────────────

    private static async Task<Guid> ObterFaturaIdAsync(HttpClient client, string competencia)
    {
        var faturas = await client.GetFromJsonAsync<FaturaListResponse>("/api/v1/faturas");
        return faturas!.Items.Single(x => x.Competencia == competencia).Id;
    }

    private static async Task<int> ObterTotalItensFaturaAsync(HttpClient client, Guid faturaId)
    {
        var resp = await client.GetFromJsonAsync<FaturaItensResponse>($"/api/v1/faturas/{faturaId}/itens?pageSize=10");
        return resp!.TotalItems;
    }

    private static async Task<Guid> ObterContaPagarIdDaFaturaAsync(HttpClient client, Guid faturaId)
    {
        var resp = await client.GetFromJsonAsync<FaturaItensResponse>($"/api/v1/faturas/{faturaId}/itens?pageSize=10");
        return resp!.Items.Single().ContaPagarId;
    }

    private sealed record FaturaListResponse(List<FaturaItem> Items, int TotalItems);
    private sealed record FaturaItem(Guid Id, string Competencia);
    private sealed record FaturaItensResponse(List<FaturaItemMin> Items, int TotalItems);
    private sealed record FaturaItemMin(Guid ContaPagarId);
    private sealed record ReembolsoResponse(List<ContaReceberResumo> ContasReceber);
    private sealed record ContaReceberResumo(Guid Id, decimal ValorLiquido);
}
