using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Api.Tests.Dashboard;

public sealed class DashboardControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task GetResumo_DeveConsolidarCardsListasMovimentosERisco()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var despesaLiquidadaId = await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-04-01",
            dataVencimento: "2026-04-02",
            valor: 100m,
            descricao: "Despesa liquidada");

        await LiquidarContaPagarAsync(client, despesaLiquidadaId, fixture.ContaBancariaId, "2026-04-02");

        var receitaLiquidadaId = await CriarContaReceberAsync(
            client,
            fixture,
            dataEmissao: "2026-04-03",
            dataVencimento: "2026-04-04",
            valor: 250m,
            descricao: "Receita recebida");

        await LiquidarContaReceberAsync(client, receitaLiquidadaId, fixture.ContaBancariaId, "2026-04-04");

        await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-04-01",
            dataVencimento: "2026-04-03",
            valor: 800m,
            descricao: "Fornecedor atrasado");

        await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-04-04",
            dataVencimento: "2026-04-08",
            valor: 700m,
            descricao: "Imposto da semana");

        await CriarContaReceberAsync(
            client,
            fixture,
            dataEmissao: "2026-04-04",
            dataVencimento: "2026-04-09",
            valor: 200m,
            descricao: "Cliente da semana");

        var resumo = await client.GetFromJsonAsync<DashboardResumoResponse>(
            "/api/v1/dashboard/resumo?dataReferencia=2026-04-05&diasProjetados=10");

        resumo.Should().NotBeNull();
        resumo!.SaldoAtual.Should().Be(1150m);
        resumo.TotalAPagar.Should().Be(1500m);
        resumo.TotalAReceber.Should().Be(200m);
        resumo.SaldoProjetado.Should().Be(-150m);
        resumo.RiscoSaldoNegativo.Should().BeTrue();
        resumo.ContasVencidas.Should().ContainSingle(item =>
            item.Descricao == "Fornecedor atrasado" &&
            item.TipoLancamento == "ContaPagar" &&
            item.StatusCodigo == "VENCIDA");
        resumo.ContasAVencer.Should().Contain(item =>
            item.Descricao == "Imposto da semana" &&
            item.TipoLancamento == "ContaPagar");
        resumo.ContasAVencer.Should().Contain(item =>
            item.Descricao == "Cliente da semana" &&
            item.TipoLancamento == "ContaReceber");
        resumo.MovimentacoesRecentes.Should().HaveCountGreaterThanOrEqualTo(2);
        resumo.MovimentacoesRecentes.First().DataMovimentacao.Should().Be(new DateOnly(2026, 4, 4));
        resumo.MovimentacoesRecentes.Should().Contain(item =>
            item.ContaReceberId == receitaLiquidadaId &&
            item.Tipo == "Entrada" &&
            item.Natureza == "Realizada" &&
            item.Valor == 250m);
        resumo.MovimentacoesRecentes.Should().Contain(item =>
            item.ContaPagarId == despesaLiquidadaId &&
            item.Tipo == "Saida" &&
            item.Natureza == "Realizada" &&
            item.Valor == 100m);
    }

    [Fact]
    public async Task GetFluxoCaixa_DeveDiferenciarVisaoCaixaEEconomicaParaCompraNoCartao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        await CriarCompraCartaoAsync(
            client,
            fixture,
            dataEmissao: "2026-04-05",
            valor: 300m,
            descricao: "Notebook no cartao");

        var fluxoCaixa = await client.GetFromJsonAsync<DashboardFluxoCaixaResponse>(
            "/api/v1/dashboard/fluxo-caixa?dataInicial=2026-04-05&dias=20&visao=Caixa");

        var fluxoEconomico = await client.GetFromJsonAsync<DashboardFluxoCaixaResponse>(
            "/api/v1/dashboard/fluxo-caixa?dataInicial=2026-04-05&dias=20&visao=Economica");

        fluxoCaixa.Should().NotBeNull();
        fluxoEconomico.Should().NotBeNull();
        fluxoCaixa!.Visao.Should().Be("Caixa");
        fluxoEconomico!.Visao.Should().Be("Economica");

        var caixaDiaCompra = fluxoCaixa.Itens.Single(item => item.Data == new DateOnly(2026, 4, 5));
        var economicoDiaCompra = fluxoEconomico.Itens.Single(item => item.Data == new DateOnly(2026, 4, 5));
        var caixaDiaVencimento = fluxoCaixa.Itens.Single(item => item.Data == new DateOnly(2026, 4, 20));
        var economicoDiaVencimento = fluxoEconomico.Itens.Single(item => item.Data == new DateOnly(2026, 4, 20));

        caixaDiaCompra.SaidasPrevistas.Should().Be(0m);
        economicoDiaCompra.SaidasPrevistas.Should().Be(300m);
        economicoDiaCompra.SaldoFinalPrevisto.Should().Be(700m);

        caixaDiaVencimento.SaidasPrevistas.Should().Be(300m);
        caixaDiaVencimento.SaldoFinalPrevisto.Should().Be(700m);
        economicoDiaVencimento.SaidasPrevistas.Should().Be(0m);
    }

    [Fact]
    public async Task GetFluxoCaixa_DeveProjetarRecorrenciasEComprasImportadasSemDuplicarParcelas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var criarRecorrenciaResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-05",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-15",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 50m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Streaming mensal",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 50m }
            },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                tipoDia = "DiaFixo",
                diaOrdemMensal = 15,
                dataInicio = "2026-04-15",
                dataFim = (string?)null,
                permiteEdicaoOcorrenciaIndividual = true
            }
        });

        criarRecorrenciaResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            AdicionarCompraCartaoImportadaConfirmada(
                dbContext,
                "NETFLIX",
                "2026-04-07",
                "2026-04-13",
                30m,
                fixture.ContaGerencialDespesaId,
                fixture.ResponsavelId,
                marcarComoRecorrente: true);
            AdicionarCompraCartaoImportadaConfirmada(
                dbContext,
                "Amazon BR 1/3",
                "2026-03-29",
                "2026-04-13",
                100m,
                fixture.ContaGerencialDespesaId,
                fixture.ResponsavelId);
            AdicionarCompraCartaoImportadaConfirmada(
                dbContext,
                "Amazon BR 2/3",
                "2026-04-29",
                "2026-05-13",
                100m,
                fixture.ContaGerencialDespesaId,
                fixture.ResponsavelId);

            await dbContext.SaveChangesAsync(default);
        }

        var fluxo = await client.GetFromJsonAsync<DashboardFluxoCaixaResponse>(
            "/api/v1/dashboard/fluxo-caixa?dataInicial=2026-05-01&dias=50&visao=Caixa");

        fluxo.Should().NotBeNull();

        var diaTrezeDeMaio = fluxo!.Itens.Single(item => item.Data == new DateOnly(2026, 5, 13));
        var diaQuinzeDeMaio = fluxo.Itens.Single(item => item.Data == new DateOnly(2026, 5, 15));
        var diaTrezeDeJunho = fluxo.Itens.Single(item => item.Data == new DateOnly(2026, 6, 13));
        var diaQuinzeDeJunho = fluxo.Itens.Single(item => item.Data == new DateOnly(2026, 6, 15));

        diaTrezeDeMaio.SaidasPrevistas.Should().Be(130m);
        diaQuinzeDeMaio.SaidasPrevistas.Should().Be(50m);
        diaTrezeDeJunho.SaidasPrevistas.Should().Be(130m);
        diaQuinzeDeJunho.SaidasPrevistas.Should().Be(50m);
    }

    [Fact]
    public async Task GetFluxoCaixa_QuandoMesReferenciaInformado_DeveRetornarMesCompletoComPrevisoes()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var criarRecorrenciaResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-05",
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-15",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 50m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Streaming mensal",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 50m }
            },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                tipoDia = "DiaFixo",
                diaOrdemMensal = 15,
                dataInicio = "2026-04-15",
                dataFim = (string?)null,
                permiteEdicaoOcorrenciaIndividual = true
            }
        });

        criarRecorrenciaResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            AdicionarCompraCartaoImportadaConfirmada(
                dbContext,
                "NETFLIX",
                "2026-04-07",
                "2026-04-13",
                30m,
                fixture.ContaGerencialDespesaId,
                fixture.ResponsavelId,
                marcarComoRecorrente: true);
            AdicionarCompraCartaoImportadaConfirmada(
                dbContext,
                "Amazon BR 1/3",
                "2026-03-29",
                "2026-04-13",
                100m,
                fixture.ContaGerencialDespesaId,
                fixture.ResponsavelId);
            AdicionarCompraCartaoImportadaConfirmada(
                dbContext,
                "Amazon BR 2/3",
                "2026-04-29",
                "2026-05-13",
                100m,
                fixture.ContaGerencialDespesaId,
                fixture.ResponsavelId);

            await dbContext.SaveChangesAsync(default);
        }

        var fluxo = await client.GetFromJsonAsync<DashboardFluxoCaixaResponse>(
            "/api/v1/dashboard/fluxo-caixa?mesReferencia=2026-05&visao=Caixa");

        fluxo.Should().NotBeNull();
        fluxo!.DataInicial.Should().Be(new DateOnly(2026, 5, 1));
        fluxo.Dias.Should().Be(31);
        fluxo.Itens.Should().HaveCount(31);
        fluxo.Itens.First().Data.Should().Be(new DateOnly(2026, 5, 1));
        fluxo.Itens.Last().Data.Should().Be(new DateOnly(2026, 5, 31));
        fluxo.Itens.Single(item => item.Data == new DateOnly(2026, 5, 13)).SaidasPrevistas.Should().Be(130m);
        fluxo.Itens.Single(item => item.Data == new DateOnly(2026, 5, 15)).SaidasPrevistas.Should().Be(50m);
    }

    [Fact]
    public async Task GetContasGerenciaisResumo_DeveConsolidarRateiosPorDataEmissao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        await CriarCompraCartaoAsync(
            client,
            fixture,
            dataEmissao: "2026-04-05",
            valor: 300m,
            descricao: "Notebook administrativo",
            contaGerencialId: fixture.ContaGerencialAdministrativaId);

        await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-04-06",
            dataVencimento: "2026-04-10",
            valor: 150m,
            descricao: "Servico operacional",
            contaGerencialId: fixture.ContaGerencialDespesaId);

        await CriarContaReceberAsync(
            client,
            fixture,
            dataEmissao: "2026-04-07",
            dataVencimento: "2026-04-08",
            valor: 500m,
            descricao: "Projeto entregue",
            contaGerencialId: fixture.ContaGerencialReceitaId);

        await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-04-20",
            dataVencimento: "2026-04-25",
            valor: 999m,
            descricao: "Fora da janela",
            contaGerencialId: fixture.ContaGerencialDespesaId);

        var resumo = await client.GetFromJsonAsync<DashboardContaGerencialResumoResponse>(
            "/api/v1/dashboard/contas-gerenciais/resumo?dataInicial=2026-04-05&dias=5");

        resumo.Should().NotBeNull();
        resumo!.DataInicial.Should().Be(new DateOnly(2026, 4, 5));
        resumo.Dias.Should().Be(5);
        resumo.TotalReceitas.Should().Be(500m);
        resumo.TotalDespesas.Should().Be(450m);
        resumo.Saldo.Should().Be(50m);
        resumo.Itens.Should().HaveCount(3);
        resumo.Itens.Should().Contain(item =>
            item.ContaGerencialId == fixture.ContaGerencialAdministrativaId &&
            item.Codigo == "ADM" &&
            item.Descricao == "Administrativo" &&
            item.Tipo == "Despesa" &&
            item.ValorTotal == 300m &&
            item.QuantidadeLancamentos == 1 &&
            item.UltimaDataLancamento == new DateOnly(2026, 4, 5));
        resumo.Itens.Should().Contain(item =>
            item.ContaGerencialId == fixture.ContaGerencialDespesaId &&
            item.Codigo == "DESP" &&
            item.Descricao == "Despesa Operacional" &&
            item.Tipo == "Despesa" &&
            item.ValorTotal == 150m &&
            item.QuantidadeLancamentos == 1 &&
            item.UltimaDataLancamento == new DateOnly(2026, 4, 6));
        resumo.Itens.Should().Contain(item =>
            item.ContaGerencialId == fixture.ContaGerencialReceitaId &&
            item.Codigo == "REC" &&
            item.Descricao == "Receita de Servicos" &&
            item.Tipo == "Receita" &&
            item.ValorTotal == 500m &&
            item.QuantidadeLancamentos == 1 &&
            item.UltimaDataLancamento == new DateOnly(2026, 4, 7));
    }

    [Fact]
    public async Task GetContasGerenciaisResumo_QuandoMesReferenciaInformado_DeveUsarMesInteiro()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        await CriarCompraCartaoAsync(
            client,
            fixture,
            dataEmissao: "2026-05-05",
            valor: 300m,
            descricao: "Notebook administrativo",
            contaGerencialId: fixture.ContaGerencialAdministrativaId);

        await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-05-06",
            dataVencimento: "2026-05-10",
            valor: 150m,
            descricao: "Servico operacional",
            contaGerencialId: fixture.ContaGerencialDespesaId);

        await CriarContaReceberAsync(
            client,
            fixture,
            dataEmissao: "2026-05-07",
            dataVencimento: "2026-05-08",
            valor: 500m,
            descricao: "Projeto entregue",
            contaGerencialId: fixture.ContaGerencialReceitaId);

        await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-06-01",
            dataVencimento: "2026-06-05",
            valor: 999m,
            descricao: "Fora do mes",
            contaGerencialId: fixture.ContaGerencialDespesaId);

        var resumo = await client.GetFromJsonAsync<DashboardContaGerencialResumoResponse>(
            "/api/v1/dashboard/contas-gerenciais/resumo?mesReferencia=2026-05");

        resumo.Should().NotBeNull();
        resumo!.DataInicial.Should().Be(new DateOnly(2026, 5, 1));
        resumo.Dias.Should().Be(31);
        resumo.TotalReceitas.Should().Be(500m);
        resumo.TotalDespesas.Should().Be(450m);
        resumo.Saldo.Should().Be(50m);
        resumo.Itens.Should().HaveCount(3);
        resumo.Itens.Should().NotContain(item => item.Descricao == "Fora do mes");
    }

    [Fact]
    public async Task GetResumo_QuandoMesReferenciaForMesAtual_DeveSepararVencidasDeAVencerPelaDataAtual()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var mesAtual = new DateOnly(hoje.Year, hoje.Month, 1);
        var mesAtualLabel = mesAtual.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var diaVencido = hoje.Day > 1 ? hoje.AddDays(-1) : hoje;
        var diaAVencer = hoje.Day < DateTime.DaysInMonth(hoje.Year, hoje.Month) ? hoje.AddDays(1) : hoje;

        await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: mesAtual.ToString("yyyy-MM-01", CultureInfo.InvariantCulture),
            dataVencimento: diaVencido.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            valor: 300m,
            descricao: "Conta vencida no mês atual",
            contaGerencialId: fixture.ContaGerencialDespesaId);

        await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: mesAtual.ToString("yyyy-MM-01", CultureInfo.InvariantCulture),
            dataVencimento: diaAVencer.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            valor: 150m,
            descricao: "Conta a vencer no mês atual",
            contaGerencialId: fixture.ContaGerencialDespesaId);

        var resumo = await client.GetFromJsonAsync<DashboardResumoResponse>(
            $"/api/v1/dashboard/resumo?mesReferencia={mesAtualLabel}");

        resumo.Should().NotBeNull();
        resumo!.ContasVencidas.Should().Contain(item =>
            item.Descricao == "Conta vencida no mês atual" &&
            item.DataVencimento == diaVencido);
        resumo.ContasVencidas.Should().NotContain(item => item.Descricao == "Conta a vencer no mês atual");
        resumo.ContasAVencer.Should().Contain(item =>
            item.Descricao == "Conta a vencer no mês atual" &&
            item.DataVencimento == diaAVencer);
        resumo.ContasAVencer.Should().NotContain(item => item.Descricao == "Conta vencida no mês atual");
    }

    [Fact]
    public async Task GetContasGerenciaisSerie_DeveFiltrarPorContaGerencial()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        await CriarCompraCartaoAsync(
            client,
            fixture,
            dataEmissao: "2026-04-05",
            valor: 300m,
            descricao: "Notebook administrativo",
            contaGerencialId: fixture.ContaGerencialAdministrativaId);

        await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-04-07",
            dataVencimento: "2026-04-10",
            valor: 50m,
            descricao: "Licenca administrativa",
            contaGerencialId: fixture.ContaGerencialAdministrativaId);

        await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-04-06",
            dataVencimento: "2026-04-10",
            valor: 150m,
            descricao: "Servico operacional",
            contaGerencialId: fixture.ContaGerencialDespesaId);

        var serie = await client.GetFromJsonAsync<DashboardContaGerencialSerieResponse>(
            $"/api/v1/dashboard/contas-gerenciais/serie?dataInicial=2026-04-05&dias=5&tipo=Despesa&contaGerencialId={fixture.ContaGerencialAdministrativaId}");

        serie.Should().NotBeNull();
        serie!.DataInicial.Should().Be(new DateOnly(2026, 4, 5));
        serie.Dias.Should().Be(5);
        serie.Tipo.Should().Be("Despesa");
        serie.ContaGerencialId.Should().Be(fixture.ContaGerencialAdministrativaId);
        serie.Itens.Should().HaveCount(5);
        serie.Itens.Should().ContainSingle(item =>
            item.Data == new DateOnly(2026, 4, 5) &&
            item.TotalDespesas == 300m &&
            item.TotalReceitas == 0m &&
            item.Saldo == -300m);
        serie.Itens.Should().ContainSingle(item =>
            item.Data == new DateOnly(2026, 4, 7) &&
            item.TotalDespesas == 50m &&
            item.TotalReceitas == 0m &&
            item.Saldo == -50m);
        serie.Itens.Should().OnlyContain(item =>
            item.Data == new DateOnly(2026, 4, 5) ||
            item.Data == new DateOnly(2026, 4, 6) ||
            item.Data == new DateOnly(2026, 4, 7) ||
            item.Data == new DateOnly(2026, 4, 8) ||
            item.Data == new DateOnly(2026, 4, 9));
    }

    [Fact]
    public async Task GetContasGerenciaisResumo_QuandoTipoForInvalido_DeveRetornarBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/dashboard/contas-gerenciais/resumo?tipo=Inexistente");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetContasGerenciaisLancamentos_DeveRetornarComposicaoDaContaNoPeriodo()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        await CriarCompraCartaoAsync(
            client,
            fixture,
            dataEmissao: "2026-04-05",
            valor: 300m,
            descricao: "Notebook administrativo",
            contaGerencialId: fixture.ContaGerencialAdministrativaId);

        var contaPagarId = await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-04-06",
            dataVencimento: "2026-04-10",
            valor: 150m,
            descricao: "Licenca administrativa",
            contaGerencialId: fixture.ContaGerencialAdministrativaId);

        await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-04-06",
            dataVencimento: "2026-04-10",
            valor: 999m,
            descricao: "Outra categoria",
            contaGerencialId: fixture.ContaGerencialDespesaId);

        var response = await client.GetFromJsonAsync<DashboardContaGerencialLancamentosResponse>(
            $"/api/v1/dashboard/contas-gerenciais/lancamentos?dataInicial=2026-04-05&dias=5&contaGerencialId={fixture.ContaGerencialAdministrativaId}");

        response.Should().NotBeNull();
        response!.DataInicial.Should().Be(new DateOnly(2026, 4, 5));
        response.Dias.Should().Be(5);
        response.ContaGerencialId.Should().Be(fixture.ContaGerencialAdministrativaId);
        response.ContaGerencialCodigo.Should().Be("ADM");
        response.ContaGerencialDescricao.Should().Be("Administrativo");
        response.Itens.Should().HaveCount(2);
        response.Itens.Should().Contain(item =>
            item.TipoLancamento == "ContaPagar" &&
            item.Descricao == "Notebook administrativo" &&
            item.PessoaNome == "Fornecedor Fase 3" &&
            item.DataEmissao == new DateOnly(2026, 4, 5) &&
            item.DataVencimento == new DateOnly(2026, 4, 20) &&
            item.ValorRateio == 300m &&
            item.ValorLancamento == 300m &&
            item.StatusCodigo == "EM_FATURA" &&
            item.StatusNome == "Em fatura");
        response.Itens.Should().Contain(item =>
            item.LancamentoId == contaPagarId &&
            item.TipoLancamento == "ContaPagar" &&
            item.Descricao == "Licenca administrativa" &&
            item.PessoaNome == "Fornecedor Fase 3" &&
            item.DataEmissao == new DateOnly(2026, 4, 6) &&
            item.DataVencimento == new DateOnly(2026, 4, 10) &&
            item.ValorRateio == 150m &&
            item.ValorLancamento == 150m &&
            item.StatusCodigo == "PENDENTE" &&
            item.StatusNome == "Pendente");
    }

    [Fact]
    public async Task GetContasGerenciaisLancamentos_QuandoContaNaoForInformada_DeveRetornarBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/dashboard/contas-gerenciais/lancamentos?dias=5");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCentralPrevisaoResumo_DeveUnificarOrigensEStatusSemDuplicarOcorrenciasSemIncluirComprasPlanejadasIsoladas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var contaPagarRecorrenteId = await CriarContaPagarRecorrenteAsync(
            client,
            fixture,
            dataEmissao: "2026-04-05",
            dataVencimento: "2026-04-15",
            valor: 50m,
            descricao: "Streaming mensal");

        await GerarOcorrenciasContaPagarAsync(client, contaPagarRecorrenteId, "2026-05-31");

        await CriarContaReceberRecorrenteAsync(
            client,
            fixture,
            dataEmissao: "2026-04-05",
            dataVencimento: "2026-04-20",
            valor: 80m,
            descricao: "Assinatura recebida");

        await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-05-01",
            dataVencimento: "2026-05-10",
            valor: 300m,
            descricao: "Notebook parcelado",
            quantidadeParcelas: 3,
            contaGerencialId: fixture.ContaGerencialDespesaId);

        _ = await CriarCompraPlanejadaAsync(
            client,
            fixture,
            "Notebook gamer",
            4500m,
            "2026-05-25");

        var compraPlanejadaConvertidaId = await CriarCompraPlanejadaAsync(
            client,
            fixture,
            "Cadeira ergonomica",
            1800m,
            "2026-05-26");

        _ = await CriarContaPagarAsync(
            client,
            fixture,
            dataEmissao: "2026-05-02",
            dataVencimento: "2026-05-26",
            valor: 1800m,
            descricao: "Cadeira ergonomica",
            contaGerencialId: fixture.ContaGerencialDespesaId,
            origemCompraPlanejadaId: compraPlanejadaConvertidaId);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            AdicionarCompraCartaoImportadaConfirmada(
                dbContext,
                "NETFLIX",
                "2026-04-07",
                "2026-04-13",
                30m,
                fixture.ContaGerencialDespesaId,
                fixture.ResponsavelId,
                marcarComoRecorrente: true);
            AdicionarCompraCartaoImportadaConfirmada(
                dbContext,
                "NETFLIX",
                "2026-05-07",
                "2026-05-13",
                30m,
                fixture.ContaGerencialDespesaId,
                fixture.ResponsavelId,
                marcarComoRecorrente: true);
            AdicionarCompraCartaoImportadaConfirmada(
                dbContext,
                "Amazon BR 1/3",
                "2026-04-29",
                "2026-04-13",
                100m,
                fixture.ContaGerencialDespesaId,
                fixture.ResponsavelId);
            AdicionarCompraCartaoImportadaConfirmada(
                dbContext,
                "Amazon BR 2/3",
                "2026-05-29",
                "2026-05-13",
                100m,
                fixture.ContaGerencialDespesaId,
                fixture.ResponsavelId);

            await dbContext.SaveChangesAsync(default);
        }

        var response = await client.GetFromJsonAsync<DashboardCentralPrevisaoResumoResponse>(
            "/api/v1/dashboard/central-previsao/resumo?mesReferencia=2026-05");

        response.Should().NotBeNull();
        response!.DataInicial.Should().Be(new DateOnly(2026, 5, 1));
        response.Dias.Should().Be(31);
        response.Itens.Should().Contain(item =>
            item.Data == new DateOnly(2026, 5, 10) &&
            item.Origem == "Parcela" &&
            item.Status == "Substituido" &&
            item.TipoMovimentacao == "Saida" &&
            item.QuantidadeItens == 1 &&
            item.ValorTotal == 100m);
        response.Itens.Should().Contain(item =>
            item.Data == new DateOnly(2026, 5, 13) &&
            item.Origem == "CompraRecorrenteImportada" &&
            item.Status == "Substituido" &&
            item.TipoMovimentacao == "Saida" &&
            item.ValorTotal == 30m);
        response.Itens.Should().Contain(item =>
            item.Data == new DateOnly(2026, 5, 13) &&
            item.Origem == "Parcela" &&
            item.Status == "Substituido" &&
            item.TipoMovimentacao == "Saida" &&
            item.ValorTotal == 100m);
        response.Itens.Should().Contain(item =>
            item.Data == new DateOnly(2026, 5, 15) &&
            item.Origem == "ContaFuturaGerada" &&
            item.Status == "Substituido" &&
            item.TipoMovimentacao == "Saida" &&
            item.ValorTotal == 50m);
        response.Itens.Should().Contain(item =>
            item.Data == new DateOnly(2026, 5, 20) &&
            item.Origem == "Recorrencia" &&
            item.Status == "Previsto" &&
            item.TipoMovimentacao == "Entrada" &&
            item.ValorTotal == 80m);
        response.Itens.Should().NotContain(item => item.Origem == "CompraPlanejada");
        response.Itens.Should().NotContain(item =>
            item.Data == new DateOnly(2026, 5, 15) &&
            item.Origem == "Recorrencia" &&
            item.Status == "Previsto");
    }

    [Fact]
    public async Task GetCentralPrevisaoResumo_QuandoMesReferenciaForFuturo_DeveMarcarComoRealizadoApenasRecorrenciasLiquidadas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var mesFuturo = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(1);
        var mesFuturoLabel = mesFuturo.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var dataEmissao = mesFuturo.ToString("yyyy-MM-01", CultureInfo.InvariantCulture);
        var dataVencimentoLiquidada = mesFuturo.ToString("yyyy-MM-07", CultureInfo.InvariantCulture);
        var dataVencimentoAberta = mesFuturo.ToString("yyyy-MM-10", CultureInfo.InvariantCulture);
        var dataInicioRecorrencia = mesFuturo.ToString("yyyy-MM-15", CultureInfo.InvariantCulture);

        var contaPagarLiquidadaId = await CriarContaPagarRecorrenteAsync(client, fixture, dataEmissao, dataVencimentoLiquidada, 81m, "Plano funerário", dataInicioRecorrencia);

        _ = await CriarContaPagarRecorrenteAsync(client, fixture, dataEmissao, dataVencimentoAberta, 300m, "Contabilidade", dataInicioRecorrencia);

        var liquidarResponse = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{contaPagarLiquidadaId}/liquidar", new
        {
            dataLiquidacao = dataVencimentoLiquidada,
            contaBancariaId = fixture.ContaBancariaId
        });

        var response = await client.GetFromJsonAsync<DashboardCentralPrevisaoResumoResponse>(
            $"/api/v1/dashboard/central-previsao/resumo?mesReferencia={mesFuturoLabel}");

        liquidarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Should().NotBeNull();
        response!.Itens.Should().Contain(item =>
            item.Data == new DateOnly(mesFuturo.Year, mesFuturo.Month, 7) &&
            item.Origem == "Recorrencia" &&
            item.Status == "Realizado" &&
            item.QuantidadeItens == 1 &&
            item.ValorTotal == 81m);
        response.Itens.Should().Contain(item =>
            item.Data == new DateOnly(mesFuturo.Year, mesFuturo.Month, 10) &&
            item.Origem == "Recorrencia" &&
            item.Status == "Substituido" &&
            item.QuantidadeItens == 1 &&
            item.ValorTotal == 300m);
        response.Itens.Should().NotContain(item =>
            item.Data == new DateOnly(mesFuturo.Year, mesFuturo.Month, 10) &&
            item.Origem == "Recorrencia" &&
            item.Status == "Realizado");
    }

    [Fact]
    public async Task GetFluxoCaixa_QuandoMesReferenciaForMesAtual_NaoDeveProjetarRecorrencias()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var mesAtual = new DateOnly(hoje.Year, hoje.Month, 1);
        var mesSeguinte = mesAtual.AddMonths(1);
        var mesAtualLabel = mesAtual.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var mesSeguinteLabel = mesSeguinte.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var dataEmissao = mesAtual.ToString("yyyy-MM-01", CultureInfo.InvariantCulture);
        var dataVencimento = mesAtual.ToString("yyyy-MM-07", CultureInfo.InvariantCulture);
        var dataInicioRecorrencia = mesAtual.ToString("yyyy-MM-15", CultureInfo.InvariantCulture);

        var responseCriacao = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao,
            recebedorId = fixture.RecebedorId,
            dataVencimento,
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 50m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Streaming mensal",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 50m }
            },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                tipoDia = "DiaFixo",
                diaOrdemMensal = 15,
                dataInicio = dataInicioRecorrencia,
                dataFim = (string?)null,
                permiteEdicaoOcorrenciaIndividual = true
            }
        });

        responseCriacao.StatusCode.Should().Be(HttpStatusCode.Created);

        var fluxoMesAtual = await client.GetFromJsonAsync<DashboardFluxoCaixaResponse>(
            $"/api/v1/dashboard/fluxo-caixa?mesReferencia={mesAtualLabel}&visao=Caixa");

        var fluxoMesSeguinte = await client.GetFromJsonAsync<DashboardFluxoCaixaResponse>(
            $"/api/v1/dashboard/fluxo-caixa?mesReferencia={mesSeguinteLabel}&visao=Caixa");

        fluxoMesAtual.Should().NotBeNull();
        fluxoMesSeguinte.Should().NotBeNull();
        fluxoMesAtual!.Itens.Single(item => item.Data == new DateOnly(mesAtual.Year, mesAtual.Month, 15)).SaidasPrevistas.Should().Be(0m);
        fluxoMesSeguinte!.Itens.Single(item => item.Data == new DateOnly(mesSeguinte.Year, mesSeguinte.Month, 15)).SaidasPrevistas.Should().Be(50m);
    }

    [Fact]
    public async Task GetCentralPrevisaoResumo_QuandoMesReferenciaForMesAtual_DeveOcultarRecorrencias()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var mesAtual = new DateOnly(hoje.Year, hoje.Month, 1);
        var mesSeguinte = mesAtual.AddMonths(1);
        var mesAtualLabel = mesAtual.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var mesSeguinteLabel = mesSeguinte.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var dataEmissao = mesAtual.ToString("yyyy-MM-01", CultureInfo.InvariantCulture);
        var dataVencimento = mesAtual.ToString("yyyy-MM-07", CultureInfo.InvariantCulture);
        var dataInicioRecorrencia = mesAtual.ToString("yyyy-MM-15", CultureInfo.InvariantCulture);

        var responseCriacao = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao,
            recebedorId = fixture.RecebedorId,
            dataVencimento,
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 81m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Plano funerário",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 81m }
            },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                tipoDia = "DiaFixo",
                diaOrdemMensal = 15,
                dataInicio = dataInicioRecorrencia,
                dataFim = (string?)null,
                permiteEdicaoOcorrenciaIndividual = true
            }
        });

        responseCriacao.StatusCode.Should().Be(HttpStatusCode.Created);

        var resumoMesAtual = await client.GetFromJsonAsync<DashboardCentralPrevisaoResumoResponse>(
            $"/api/v1/dashboard/central-previsao/resumo?mesReferencia={mesAtualLabel}");

        var resumoMesSeguinte = await client.GetFromJsonAsync<DashboardCentralPrevisaoResumoResponse>(
            $"/api/v1/dashboard/central-previsao/resumo?mesReferencia={mesSeguinteLabel}");

        resumoMesAtual.Should().NotBeNull();
        resumoMesSeguinte.Should().NotBeNull();
        resumoMesAtual!.Itens.Should().NotContain(item =>
            item.Origem == "Recorrencia" ||
            item.Origem == "ContaFuturaGerada");
        resumoMesSeguinte!.Itens.Should().Contain(item =>
            item.Data == new DateOnly(mesSeguinte.Year, mesSeguinte.Month, 15) &&
            item.Origem == "Recorrencia" &&
            item.Status == "Previsto" &&
            item.ValorTotal == 81m);
    }

    [Fact]
    public async Task GetCentralPrevisaoItens_QuandoOrigemForCompraPlanejada_DeveRetornarVazio()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        _ = await CriarCompraPlanejadaAsync(
            client,
            fixture,
            "Notebook gamer",
            4500m,
            "2026-05-25");

        var response = await client.GetFromJsonAsync<DashboardCentralPrevisaoItensResponse>(
            "/api/v1/dashboard/central-previsao/itens?mesReferencia=2026-05&data=2026-05-25&origem=CompraPlanejada&status=Previsto");

        response.Should().NotBeNull();
        response!.DataInicial.Should().Be(new DateOnly(2026, 5, 1));
        response.Dias.Should().Be(31);
        response.Data.Should().Be(new DateOnly(2026, 5, 25));
        response.Origem.Should().Be("CompraPlanejada");
        response.Status.Should().Be("Previsto");
        response.Itens.Should().BeEmpty();
    }

    private static async Task<Guid> CriarContaPagarAsync(
        HttpClient client,
        FinancialFixtureSeed.FixtureIds fixture,
        string dataEmissao,
        string dataVencimento,
        decimal valor,
        string descricao,
        Guid? contaGerencialId = null,
        int quantidadeParcelas = 1,
        Guid? origemCompraPlanejadaId = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao,
            recebedorId = fixture.RecebedorId,
            dataVencimento,
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = valor,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas,
            descricao,
            origemCompraPlanejadaId,
            rateios = new[]
            {
                new { contaGerencialId = contaGerencialId ?? fixture.ContaGerencialDespesaId, valor }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private static async Task<Guid> CriarContaPagarRecorrenteAsync(
        HttpClient client,
        FinancialFixtureSeed.FixtureIds fixture,
        string dataEmissao,
        string dataVencimento,
        decimal valor,
        string descricao,
        string dataInicioRecorrencia = "2026-04-15")
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao,
            recebedorId = fixture.RecebedorId,
            dataVencimento,
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = valor,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao,
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor }
            },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                tipoDia = "DiaFixo",
                diaOrdemMensal = 15,
                dataInicio = dataInicioRecorrencia,
                dataFim = (string?)null,
                permiteEdicaoOcorrenciaIndividual = true
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private static async Task<Guid> CriarContaReceberRecorrenteAsync(
        HttpClient client,
        FinancialFixtureSeed.FixtureIds fixture,
        string dataEmissao,
        string dataVencimento,
        decimal valor,
        string descricao,
        string dataInicioRecorrencia = "2026-04-20")
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao,
            pagadorId = fixture.PagadorId,
            dataVencimento,
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = valor,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao,
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor }
            },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                tipoDia = "DiaFixo",
                diaOrdemMensal = 20,
                dataInicio = dataInicioRecorrencia,
                dataFim = (string?)null,
                permiteEdicaoOcorrenciaIndividual = true
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private static async Task GerarOcorrenciasContaPagarAsync(HttpClient client, Guid contaPagarId, string ateData)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{contaPagarId}/gerar-ocorrencias", new
        {
            ateData
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<Guid> CriarCompraPlanejadaAsync(
        HttpClient client,
        FinancialFixtureSeed.FixtureIds fixture,
        string titulo,
        decimal valorEstimado,
        string dataDesejada)
    {
        var response = await client.PostAsJsonAsync("/api/v1/compras-planejadas", new
        {
            titulo,
            descricao = "Compra futura monitorada",
            valorEstimado,
            dataDesejada,
            prioridade = "Alta",
            status = "Planejada",
            parcelavel = true,
            quantidadeParcelasDesejada = 10,
            contaGerencialId = fixture.ContaGerencialDespesaId,
            responsavelId = fixture.ResponsavelId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private static async Task<Guid> CriarContaReceberAsync(
        HttpClient client,
        FinancialFixtureSeed.FixtureIds fixture,
        string dataEmissao,
        string dataVencimento,
        decimal valor,
        string descricao,
        Guid? contaGerencialId = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao,
            pagadorId = fixture.PagadorId,
            dataVencimento,
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = valor,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao,
            rateios = new[]
            {
                new { contaGerencialId = contaGerencialId ?? fixture.ContaGerencialReceitaId, valor }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private static async Task<Guid> CriarCompraCartaoAsync(
        HttpClient client,
        FinancialFixtureSeed.FixtureIds fixture,
        string dataEmissao,
        decimal valor,
        string descricao,
        Guid? contaGerencialId = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao,
            responsavelCompraId = fixture.ResponsavelId,
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-20",
            formaPagamentoId = fixture.FormaPagamentoCartaoId,
            cartaoId = fixture.CartaoId,
            contaBancariaId = (string?)null,
            valorOriginal = valor,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao,
            observacao = "Compra via cartao",
            rateios = new[]
            {
                new { contaGerencialId = contaGerencialId ?? fixture.ContaGerencialDespesaId, valor }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private static async Task LiquidarContaPagarAsync(
        HttpClient client,
        Guid contaPagarId,
        Guid contaBancariaId,
        string dataLiquidacao)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/contas-pagar/{contaPagarId}/liquidar", new
        {
            dataLiquidacao,
            contaBancariaId
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task LiquidarContaReceberAsync(
        HttpClient client,
        Guid contaReceberId,
        Guid contaBancariaId,
        string dataLiquidacao)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/contas-receber/{contaReceberId}/liquidar", new
        {
            dataLiquidacao,
            contaBancariaId
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static void AdicionarCompraCartaoImportadaConfirmada(
        IAppDbContext dbContext,
        string descricao,
        string dataIdentificada,
        string dataVencimento,
        decimal valor,
        Guid contaGerencialId,
        Guid responsavelId,
        bool marcarComoRecorrente = false)
    {
        var importacao = ImportacaoWhatsapp.CriarRecebida(
            TipoOrigemImportacaoWhatsapp.Pdf,
            "5511999998888",
            descricao,
            "fatura.pdf",
            null,
            "application/pdf");

        importacao.MarcarEmProcessamento();
        importacao.RegistrarExtracaoComSucesso(0.95m);

        var payload =
            $$"""
            {
              "descricao":"{{descricao}}",
              "valor":{{valor.ToString(CultureInfo.InvariantCulture)}},
              "dataIdentificada":"{{dataIdentificada}}",
              "dataVencimento":"{{dataVencimento}}",
              "tipoMovimentacaoSugerido":"Saida",
              "emissor":"BRADESCO",
              "cartaoFinal":"2892",
              "portador":"Cliente Exemplo"
            }
            """;

        var item = ItemImportadoWhatsapp.Criar(
            importacao.Id,
            TipoSugestaoImportacaoWhatsapp.CompraCartao,
            payload,
            null);

        importacao.SubstituirItens([item]);
        item.Confirmar("Compra importada confirmada", null, contaGerencialId, responsavelId, null, marcarComoRecorrente);
        importacao.AtualizarStatusRevisao();
        importacao.AprovarRevisao();

        dbContext.ImportacoesWhatsapp.Add(importacao);
        dbContext.ItensImportadosWhatsapp.Add(item);
    }

    private sealed record IdResponse(Guid Id);

    private sealed record DashboardResumoResponse(
        decimal SaldoAtual,
        decimal TotalAPagar,
        decimal TotalAReceber,
        decimal SaldoProjetado,
        bool RiscoSaldoNegativo,
        IReadOnlyCollection<DashboardContaResumoResponse> ContasVencidas,
        IReadOnlyCollection<DashboardContaResumoResponse> ContasAVencer,
        IReadOnlyCollection<DashboardMovimentacaoResumoResponse> MovimentacoesRecentes);

    private sealed record DashboardContaResumoResponse(
        Guid Id,
        string TipoLancamento,
        string Descricao,
        string PessoaNome,
        DateOnly DataVencimento,
        decimal Valor,
        string StatusCodigo,
        string StatusNome);

    private sealed record DashboardMovimentacaoResumoResponse(
        Guid Id,
        DateOnly DataMovimentacao,
        string Tipo,
        string Natureza,
        decimal Valor,
        string? Observacao,
        Guid? ContaPagarId,
        Guid? ContaReceberId,
        Guid? FaturaCartaoId);

    private sealed record DashboardFluxoCaixaResponse(
        string Visao,
        DateOnly DataInicial,
        int Dias,
        bool RiscoSaldoNegativo,
        IReadOnlyCollection<DashboardFluxoCaixaDiaResponse> Itens);

    private sealed record DashboardFluxoCaixaDiaResponse(
        DateOnly Data,
        decimal SaldoInicial,
        decimal EntradasPrevistas,
        decimal SaidasPrevistas,
        decimal SaldoFinalPrevisto,
        bool RiscoSaldoNegativo);

    private sealed record DashboardContaGerencialResumoResponse(
        DateOnly DataInicial,
        int Dias,
        decimal TotalReceitas,
        decimal TotalDespesas,
        decimal Saldo,
        IReadOnlyCollection<DashboardContaGerencialResumoItemResponse> Itens);

    private sealed record DashboardContaGerencialResumoItemResponse(
        Guid ContaGerencialId,
        string? Codigo,
        string Descricao,
        string Tipo,
        decimal ValorTotal,
        int QuantidadeLancamentos,
        DateOnly UltimaDataLancamento);

    private sealed record DashboardContaGerencialSerieResponse(
        DateOnly DataInicial,
        int Dias,
        string? Tipo,
        Guid? ContaGerencialId,
        IReadOnlyCollection<DashboardContaGerencialSerieDiaResponse> Itens);

    private sealed record DashboardContaGerencialSerieDiaResponse(
        DateOnly Data,
        decimal TotalReceitas,
        decimal TotalDespesas,
        decimal Saldo);

    private sealed record DashboardContaGerencialLancamentosResponse(
        DateOnly DataInicial,
        int Dias,
        string? Tipo,
        Guid ContaGerencialId,
        string? ContaGerencialCodigo,
        string ContaGerencialDescricao,
        IReadOnlyCollection<DashboardContaGerencialLancamentoItemResponse> Itens);

    private sealed record DashboardContaGerencialLancamentoItemResponse(
        Guid LancamentoId,
        string TipoLancamento,
        string Descricao,
        string PessoaNome,
        DateOnly DataEmissao,
        DateOnly DataVencimento,
        decimal ValorLancamento,
        decimal ValorRateio,
        string StatusCodigo,
        string StatusNome);

    private sealed record DashboardCentralPrevisaoResumoResponse(
        DateOnly DataInicial,
        int Dias,
        string? Origem,
        string? Status,
        IReadOnlyCollection<DashboardCentralPrevisaoResumoItemResponse> Itens);

    private sealed record DashboardCentralPrevisaoResumoItemResponse(
        DateOnly Data,
        string TipoMovimentacao,
        string Origem,
        string Status,
        int QuantidadeItens,
        decimal ValorTotal);

    private sealed record DashboardCentralPrevisaoItensResponse(
        DateOnly DataInicial,
        int Dias,
        DateOnly? Data,
        string? Origem,
        string? Status,
        IReadOnlyCollection<DashboardCentralPrevisaoItemResponse> Itens);

    private sealed record DashboardCentralPrevisaoItemResponse(
        string TipoReferencia,
        Guid ReferenciaId,
        DateOnly Data,
        string TipoMovimentacao,
        string Origem,
        string Status,
        string Descricao,
        decimal Valor,
        string? PessoaNome,
        string? ResponsavelNome,
        Guid? ContaGerencialId,
        string? ContaGerencialCodigo,
        string? ContaGerencialDescricao);
}
