using System.Net;
using System.Net.Http.Json;
using System.Text;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Financeiro.Faturas;
using ControleFinanceiro.Application.ImportacoesWhatsapp;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControleFinanceiro.Api.Tests.Importacoes;

public sealed class ImportacoesWhatsappControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly Guid FamiliaDesenvolvimentoId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task PostWebhookEGetDetalhe_DeveReceberProcessarEListarImportacaoPendenteDeRevisao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var webhookResponse = await client.PostAsJsonAsync("/api/v1/importacoes-whatsapp/webhook", new
        {
            tipoOrigem = "Texto",
            remetente = "5511988887777",
            textoBruto = "Pagar boleto academia 120,50 vencimento 2026-04-12"
        });

        webhookResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var detalhe = await webhookResponse.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        detalhe.Should().NotBeNull();
        detalhe!.StatusCodigo.Should().Be("PENDENTE_REVISAO");
        detalhe.ConfiancaExtracao.Should().BeGreaterThan(0m);
        detalhe.Itens.Should().ContainSingle(item => item.TipoSugestaoCodigo == "CONTA_PAGAR");

        var listagem = await client.GetFromJsonAsync<PagedResponse<ImportacaoWhatsappResumoResponse>>("/api/v1/importacoes-whatsapp?search=academia");

        listagem.Should().NotBeNull();
        listagem!.Items.Should().ContainSingle(item =>
            item.Id == detalhe.Id &&
            item.Remetente == "5511988887777" &&
            item.StatusCodigo == "PENDENTE_REVISAO");

        var detalheObtido = await client.GetFromJsonAsync<ImportacaoWhatsappDetalheResponse>($"/api/v1/importacoes-whatsapp/{detalhe.Id}");

        detalheObtido.Should().NotBeNull();
        detalheObtido!.TextoBruto.Should().Contain("academia");
    }

    [Fact]
    public async Task ConfirmarRejeitarEReprocessar_DeveAtualizarItensESeguirPendenteAteAprovacao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var webhookResponse = await client.PostAsJsonAsync("/api/v1/importacoes-whatsapp/webhook", new
        {
            tipoOrigem = "Texto",
            remetente = "5511977776666",
            textoBruto = "Recebido pix cliente 80,00"
        });

        var detalhe = await webhookResponse.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        detalhe.Should().NotBeNull();

        var confirmarResponse = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/itens/{detalhe!.Itens.Single().Id}/confirmar",
            new
            {
                observacao = "Lancamento validado manualmente"
            });

        confirmarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detalheConfirmado = await confirmarResponse.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        detalheConfirmado.Should().NotBeNull();
        detalheConfirmado!.StatusCodigo.Should().Be("PENDENTE_REVISAO");
        detalheConfirmado.Itens.Should().ContainSingle(item => item.StatusCodigo == "CONFIRMADO");

        var webhookRejeicaoResponse = await client.PostAsJsonAsync("/api/v1/importacoes-whatsapp/webhook", new
        {
            tipoOrigem = "Texto",
            remetente = "5511966665555",
            textoBruto = "Compra cartao supermercado 210,30"
        });

        var detalheRejeicao = await webhookRejeicaoResponse.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        detalheRejeicao.Should().NotBeNull();

        var rejeitarResponse = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/itens/{detalheRejeicao!.Itens.Single().Id}/rejeitar",
            new
            {
                observacao = "Nao corresponde ao documento esperado"
            });

        rejeitarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detalheRejeitado = await rejeitarResponse.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        detalheRejeitado.Should().NotBeNull();
        detalheRejeitado!.StatusCodigo.Should().Be("PENDENTE_REVISAO");
        detalheRejeitado.Itens.Should().ContainSingle(item => item.StatusCodigo == "REJEITADO");

        var reprocessarResponse = await client.PostAsync($"/api/v1/importacoes-whatsapp/{detalheRejeicao.Id}/reprocessar", null);

        reprocessarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detalheReprocessado = await reprocessarResponse.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        detalheReprocessado.Should().NotBeNull();
        detalheReprocessado!.StatusCodigo.Should().Be("PENDENTE_REVISAO");
        detalheReprocessado.Itens.Should().ContainSingle(item => item.StatusCodigo == "SUGERIDO");
    }

    [Fact]
    public async Task ConfirmarImportacaoEAposReabrir_DeveManterItemConfirmadoEPermitirNovaEdicao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var detalhe = await ReceberImportacaoAsync(client, "Recebido pix cliente 80,00");
        detalhe.Should().NotBeNull();
        var itemId = detalhe!.Itens.Single().Id;

        var confirmarItem = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/itens/{itemId}/confirmar",
            new
            {
                observacao = "Revisado"
            });

        confirmarItem.EnsureSuccessStatusCode();

        var aprovarImportacao = await client.PostAsync($"/api/v1/importacoes-whatsapp/{detalhe.Id}/confirmar", null);
        aprovarImportacao.StatusCode.Should().Be(HttpStatusCode.OK);

        var detalheAprovado = await aprovarImportacao.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        detalheAprovado.Should().NotBeNull();
        detalheAprovado!.StatusCodigo.Should().Be("CONFIRMADO");

        var tentarEditarItemAprovado = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/itens/{itemId}/confirmar",
            new
            {
                observacao = "Tentativa apos aprovar"
            });

        tentarEditarItemAprovado.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var reabrirImportacao = await client.PostAsync($"/api/v1/importacoes-whatsapp/{detalhe.Id}/reabrir", null);
        reabrirImportacao.StatusCode.Should().Be(HttpStatusCode.OK);

        var detalheReaberto = await reabrirImportacao.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        detalheReaberto.Should().NotBeNull();
        detalheReaberto!.StatusCodigo.Should().Be("PENDENTE_REVISAO");
        detalheReaberto.Itens.Should().ContainSingle(item =>
            item.StatusCodigo == "CONFIRMADO" &&
            item.Observacao == "Revisado");

        var editarAposReabrir = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/itens/{itemId}/confirmar",
            new
            {
                observacao = "Revisao apos reabrir"
            });

        editarAposReabrir.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostWebhook_ComMimeTypeNaoSuportado_DeveRetornarBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/importacoes-whatsapp/webhook", new
        {
            tipoOrigem = "Arquivo",
            remetente = "5511955554444",
            nomeArquivo = "planilha.exe",
            mimeType = "application/x-msdownload",
            arquivoBase64 = Convert.ToBase64String("conteudo invalido"u8.ToArray())
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostWebhook_QuandoExtratorLancarExcecao_DevePersistirErroExtracaoSemEstourar500()
    {
        await using var failingFactory = new CustomWebApplicationFactory(services =>
        {
            services.RemoveAll<IDocumentExtractor>();
            services.AddScoped<IDocumentExtractor, ThrowingDocumentExtractor>();
        });
        using var client = failingFactory.CreateClient();

        var webhookResponse = await client.PostAsJsonAsync("/api/v1/importacoes-whatsapp/webhook", new
        {
            tipoOrigem = "Texto",
            remetente = "5511944440000",
            textoBruto = "Extrato pix recebido cliente 80,00 2026-04-08"
        });

        webhookResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var detalhe = await webhookResponse.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        detalhe.Should().NotBeNull();
        detalhe!.StatusCodigo.Should().Be("ERRO_EXTRACAO");
        detalhe.MensagemErro.Should().Be("Falha ao integrar com o extrator ou a heuristica da importacao.");
        detalhe.Itens.Should().BeEmpty();
    }

    [Fact]
    public async Task PostWebhook_ComFaturaPdfBradesco_DeveGerarItensCompraCartaoPorTransacao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var pdfBytes = CreatePlainTextPseudoPdf(
        [
            "XXXX.XXXX.XXXX.1111",
            "Aplicativo Bradesco Cartoes",
            "Data: 03/04/2026 - 07:54",
            "Vencimento",
            "13/04/2026",
            "Situacao do Extrato: FECHADO",
            "CLIENTE EXEMPLO - VISA INFINITE",
            "Data",
            "Historico",
            "Moeda",
            "de",
            "origem",
            "US$",
            "Cotacao",
            "US$",
            "R$",
            "31/03",
            "SUPERMERCADO MODELO",
            "258,55",
            "29/03",
            "AMAZON BR 1/2",
            "223,40",
            "07/03",
            "OPENAI *CHATGPT SUBSCR",
            "USD",
            "20,00",
            "20,00",
            "R$ 5,57",
            "111,40"
        ]);

        var webhookResponse = await client.PostAsJsonAsync("/api/v1/importacoes-whatsapp/webhook", new
        {
            tipoOrigem = "Pdf",
            remetente = "5511933332222",
            nomeArquivo = "fatura-bradesco.pdf",
            mimeType = "application/pdf",
            arquivoBase64 = Convert.ToBase64String(pdfBytes)
        });

        webhookResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var detalhe = await webhookResponse.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        detalhe.Should().NotBeNull();
        detalhe!.StatusCodigo.Should().Be("PENDENTE_REVISAO");
        detalhe.Itens.Should().HaveCount(3);
        detalhe.Itens.Should().OnlyContain(item => item.TipoSugestaoCodigo == "COMPRA_CARTAO");
        detalhe.Itens.Should().Contain(item => item.PayloadSugeridoJson.Contains("\"emissor\":\"BRADESCO\"", StringComparison.Ordinal));
        detalhe.Itens.Should().Contain(item => item.PayloadSugeridoJson.Contains("\"dataIdentificada\":\"2026-03-31\"", StringComparison.Ordinal));
        detalhe.Itens.Should().Contain(item => item.PayloadSugeridoJson.Contains("\"dataVencimento\":\"2026-04-13\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConfirmarItemCompraCartaoSemVencimentoNoPayload_ComContaReceber_DeveUsarVencimentoInformadoNaRevisao()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var contaGerencialDespesaId = await CreateContaGerencialAsync(client, "DESP.BRAD", "Compras para terceiros", "Despesa");
        var responsavelId = await CreatePessoaAsync(client, "Mirce", "Fisica");
        var contaGerencialReceitaPadraoId = await CreateContaGerencialAsync(
            client,
            "REC.DIVIDA",
            "Recebimento de dívida",
            "Receita",
            true);
        await CreateFormaPagamentoAsync(client, "Pix", "Pix", false, false);

        var pdfBytes = CreatePlainTextPseudoPdf(
        [
            "XXXX.XXXX.XXXX.1111",
            "Aplicativo Bradesco Cartoes",
            "Data: 03/04/2026 - 07:54",
            "Situacao do Extrato: FECHADO",
            "CLIENTE EXEMPLO - VISA INFINITE",
            "24/03",
            "ZP *STUDIO CO46667 1/10",
            "430,00"
        ]);

        var webhookResponse = await client.PostAsJsonAsync("/api/v1/importacoes-whatsapp/webhook", new
        {
            tipoOrigem = "Pdf",
            remetente = "bradesco-modelo",
            nomeArquivo = "fatura-bradesco.pdf",
            mimeType = "application/pdf",
            arquivoBase64 = Convert.ToBase64String(pdfBytes)
        });

        webhookResponse.EnsureSuccessStatusCode();
        var detalhe = await webhookResponse.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        detalhe.Should().NotBeNull();
        var itemId = detalhe!.Itens.Single().Id;

        var confirmarResponse = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/itens/{itemId}/confirmar",
            new
            {
                observacao = "Compra para terceiro",
                descricaoAjustada = "Mirce - ZP Studio 1/10",
                contaGerencialId = contaGerencialDespesaId,
                responsavelId,
                dataVencimentoContaReceber = "2026-04-13",
                gerarContaReceber = true,
                marcarComoRecorrente = false
            });

        confirmarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detalheConfirmado = await confirmarResponse.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        detalheConfirmado.Should().NotBeNull();
        detalheConfirmado!.Itens.Single().ContaReceberId.Should().NotBeNull();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var contaReceber = dbContext.ContasReceber.Single(x => x.Id == detalheConfirmado.Itens.Single().ContaReceberId!.Value);
        contaReceber.DataVencimento.Should().Be(new DateOnly(2026, 4, 13));

        var rateio = dbContext.RateiosContaGerencial.Single(x => x.ContaReceberId == contaReceber.Id);
        rateio.ContaGerencialId.Should().Be(contaGerencialReceitaPadraoId);
    }

    [Fact]
    public async Task ConfirmarItemCompraCartao_ComCategoriaResponsavelEContaReceber_DevePersistirClassificacaoEGerarContaReceber()
    {
        await using var factory = new CustomWebApplicationFactory(services =>
        {
            services.RemoveAll<IImportSuggestionService>();
            services.AddScoped<IImportSuggestionService>(_ =>
                new FixedImportSuggestionService("""
                {
                  "descricao":"Mercado para familia",
                  "valor":230.75,
                  "dataIdentificada":"2026-04-02",
                  "dataVencimento":"2026-04-13",
                  "tipoMovimentacaoSugerido":"Saida",
                  "emissor":"NUBANK",
                  "cartaoFinal":"4835",
                  "portador":"Cliente Exemplo"
                }
                """));
        });

        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var contaGerencialDespesaId = await CreateContaGerencialAsync(client, "DESP.MERC", "Supermercado", "Despesa");
        var contaGerencialReceitaPadraoId = await CreateContaGerencialAsync(
            client,
            "REC.DIV",
            "Recebimento de divida",
            "Receita",
            true);
        var responsavelId = await CreatePessoaAsync(client, "Pessoa Reembolsavel", "Fisica");
        await CreateFormaPagamentoAsync(client, "Pix recebimento", "Pix", false, false);

        var webhookResponse = await client.PostAsJsonAsync("/api/v1/importacoes-whatsapp/webhook", new
        {
            tipoOrigem = "Texto",
            remetente = "5511912345678",
            textoBruto = "Compra cartao mercado para familia 230,75"
        });

        webhookResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var detalhe = await webhookResponse.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        detalhe.Should().NotBeNull();

        var confirmarResponse = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/itens/{detalhe!.Itens.Single().Id}/confirmar",
            new
            {
                observacao = "Compra feita para terceiro",
                descricaoAjustada = "Mercado da familia",
                contaGerencialId = contaGerencialDespesaId,
                responsavelId,
                gerarContaReceber = true,
                marcarComoRecorrente = true
            });

        confirmarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detalheConfirmado = await confirmarResponse.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        detalheConfirmado.Should().NotBeNull();
        detalheConfirmado!.StatusCodigo.Should().Be("PENDENTE_REVISAO");

        var itemConfirmado = detalheConfirmado.Itens.Single();
        itemConfirmado.StatusCodigo.Should().Be("CONFIRMADO");
        itemConfirmado.ContaGerencialId.Should().Be(contaGerencialDespesaId);
        itemConfirmado.ResponsavelId.Should().Be(responsavelId);
        itemConfirmado.ContaReceberId.Should().NotBeNull();
        itemConfirmado.ContaGerencialDescricao.Should().Be("Supermercado");
        itemConfirmado.ResponsavelNome.Should().Be("Pessoa Reembolsavel");
        itemConfirmado.DescricaoAjustada.Should().Be("Mercado da familia");
        itemConfirmado.MarcarComoRecorrente.Should().BeTrue();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var contaReceber = dbContext.ContasReceber.Single(x => x.Id == itemConfirmado.ContaReceberId!.Value);
        contaReceber.Descricao.Should().Be("Mercado da familia");
        contaReceber.PagadorId.Should().Be(responsavelId);
        contaReceber.ResponsavelId.Should().Be(responsavelId);
        contaReceber.ValorLiquido.Should().Be(230.75m);
        contaReceber.Origem.Should().Be(Domain.Financeiro.OrigemLancamento.Importacao);

        var rateio = dbContext.RateiosContaGerencial.Single(x => x.ContaReceberId == contaReceber.Id);
        rateio.ContaGerencialId.Should().Be(contaGerencialReceitaPadraoId);
        rateio.Valor.Should().Be(230.75m);
    }

    [Fact]
    public async Task ConfirmarItemCompraCartao_DeveExigirContaGerencialEResponsavel()
    {
        await using var factory = new CustomWebApplicationFactory(services =>
        {
            services.RemoveAll<IImportSuggestionService>();
            services.AddScoped<IImportSuggestionService>(_ =>
                new FixedTypedImportSuggestionService(
                    TipoSugestaoImportacaoWhatsapp.CompraCartao,
                    """
                    {
                      "descricao":"Amazon BR 1/2",
                      "valor":123.45,
                      "dataIdentificada":"2026-04-02",
                      "dataVencimento":"2026-04-13",
                      "tipoMovimentacaoSugerido":"Saida"
                    }
                    """));
        });

        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var detalhe = await ReceberImportacaoAsync(client, "Compra cartao amazon 123,45");
        var itemId = detalhe!.Itens.Single().Id;
        var contaGerencialId = await CreateContaGerencialAsync(client, "DESP.AMAZ", "Marketplace", "Despesa");
        var responsavelId = await CreatePessoaAsync(client, "Comprador", "Fisica");

        var responseSemConta = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/itens/{itemId}/confirmar",
            new
            {
                observacao = "Sem conta",
                responsavelId
            });

        var errorSemConta = await responseSemConta.Content.ReadFromJsonAsync<ApiErrorResponse>();

        responseSemConta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        errorSemConta.Should().NotBeNull();
        errorSemConta!.Errors.Should().ContainKey("ContaGerencialId");

        var responseSemResponsavel = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/itens/{itemId}/confirmar",
            new
            {
                observacao = "Sem responsavel",
                contaGerencialId
            });

        var errorSemResponsavel = await responseSemResponsavel.Content.ReadFromJsonAsync<ApiErrorResponse>();

        responseSemResponsavel.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        errorSemResponsavel.Should().NotBeNull();
        errorSemResponsavel!.Errors.Should().ContainKey("ResponsavelId");
    }

    [Fact]
    public async Task ConfirmarItemAntesDaAprovacao_DevePermitirEditarRevisao()
    {
        await using var factory = new CustomWebApplicationFactory(services =>
        {
            services.RemoveAll<IImportSuggestionService>();
            services.AddScoped<IImportSuggestionService>(_ =>
                new FixedTypedImportSuggestionService(
                    TipoSugestaoImportacaoWhatsapp.ItemExtrato,
                    """
                    {
                      "descricao":"PIX recebido cliente",
                      "valor":80.00,
                      "dataIdentificada":"2026-04-08",
                      "tipoMovimentacaoSugerido":"Entrada"
                    }
                    """));
        });

        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var detalhe = await ReceberImportacaoAsync(client, "Extrato pix cliente 80,00");
        var itemId = detalhe!.Itens.Single().Id;

        var primeiraConfirmacao = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/itens/{itemId}/confirmar",
            new
            {
                observacao = "Primeira revisao"
            });

        primeiraConfirmacao.StatusCode.Should().Be(HttpStatusCode.OK);

        var segundaConfirmacao = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/itens/{itemId}/confirmar",
            new
            {
                observacao = "Revisao ajustada antes da conciliacao"
            });

        segundaConfirmacao.StatusCode.Should().Be(HttpStatusCode.OK);
        var detalheAtualizado = await segundaConfirmacao.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();

        detalheAtualizado.Should().NotBeNull();
        detalheAtualizado!.Itens.Single().StatusCodigo.Should().Be("CONFIRMADO");
        detalheAtualizado.Itens.Single().Observacao.Should().Be("Revisao ajustada antes da conciliacao");
        detalheAtualizado.Itens.Single().MovimentacaoFinanceiraId.Should().BeNull();
        detalheAtualizado.StatusCodigo.Should().Be("PENDENTE_REVISAO");
    }

    [Fact]
    public async Task GetDetalhe_QuandoCompraCartaoParceladaJaTiverHistorico_DeveMarcarComoPrevisto()
    {
        await using var factory = new CustomWebApplicationFactory(services =>
        {
            services.RemoveAll<IImportSuggestionService>();
            services.AddSingleton<IImportSuggestionService>(
                new SequencedImportSuggestionService(
                [
                    new ImportSuggestionItem(
                        TipoSugestaoImportacaoWhatsapp.CompraCartao,
                        """
                        {
                          "descricao":"Amazon BR 1/10",
                          "valor":223.40,
                          "dataIdentificada":"2026-03-29",
                          "dataVencimento":"2026-04-13",
                          "tipoMovimentacaoSugerido":"Saida",
                          "emissor":"BRADESCO",
                          "cartaoFinal":"2892",
                          "portador":"Cliente Exemplo"
                        }
                        """),
                    new ImportSuggestionItem(
                        TipoSugestaoImportacaoWhatsapp.CompraCartao,
                        """
                        {
                          "descricao":"Amazon BR 2/10",
                          "valor":223.40,
                          "dataIdentificada":"2026-04-29",
                          "dataVencimento":"2026-05-13",
                          "tipoMovimentacaoSugerido":"Saida",
                          "emissor":"BRADESCO",
                          "cartaoFinal":"2892",
                          "portador":"Cliente Exemplo"
                        }
                        """)
                ]));
        });

        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var contaGerencialDespesaId = await CreateContaGerencialAsync(client, "DESP.AMAZ", "Marketplace", "Despesa");
        var responsavelId = await CreatePessoaAsync(client, "Comprador Parcelado", "Fisica");
        var recebedorFaturaId = await CreatePessoaAsync(client, "Bradesco", "Juridica");
        var formaPagamentoCartaoId = await CreateFormaPagamentoAsync(client, "Cartao de credito", "Credito", true, false);
        var cartaoId = await CreateCartaoAsync(client, "Cartao Bradesco", "Visa", "2892", 5, 13);

        var primeiraImportacao = await ReceberImportacaoAsync(client, "Fatura parcela 1");
        var confirmarPrimeira = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/itens/{primeiraImportacao!.Itens.Single().Id}/confirmar",
            new
            {
                observacao = "Primeira parcela",
                contaGerencialId = contaGerencialDespesaId,
                responsavelId
            });

        confirmarPrimeira.EnsureSuccessStatusCode();
        var aprovarPrimeira = await client.PostAsJsonAsync($"/api/v1/importacoes-whatsapp/{primeiraImportacao.Id}/confirmar", new
        {
            recebedorFaturaId,
            responsavelPagamentoFaturaId = responsavelId,
            cartaoIds = new[] { cartaoId }
        });
        aprovarPrimeira.EnsureSuccessStatusCode();

        var segundaImportacao = await ReceberImportacaoAsync(client, "Fatura parcela 2");

        segundaImportacao.Should().NotBeNull();
        segundaImportacao!.Itens.Single().StatusPrevisaoCodigo.Should().Be("PREVISTO");
        segundaImportacao.Itens.Single().StatusPrevisaoNome.Should().Be("Previsto");
    }

    [Fact]
    public async Task GetDetalhe_QuandoHouverHistoricoSemelhante_DevePreverContaGerencialEResponsavel()
    {
        await using var factory = new CustomWebApplicationFactory(services =>
        {
            services.RemoveAll<IImportSuggestionService>();
            services.AddScoped<IImportSuggestionService>(_ =>
                new FixedImportSuggestionService("""
                {
                  "descricao":"Farmacia popular",
                  "valor":89.90,
                  "dataIdentificada":"2026-04-04",
                  "dataVencimento":"2026-04-13",
                  "tipoMovimentacaoSugerido":"Saida",
                  "emissor":"BRADESCO",
                  "cartaoFinal":"2892",
                  "portador":"Cliente Exemplo"
                }
                """));
        });

        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var contaGerencialDespesaId = await CreateContaGerencialAsync(client, "DESP.SAU", "Farmacia", "Despesa");
        await CreateContaGerencialAsync(client, "REC.DIV", "Recebimento de divida", "Receita", true);
        var responsavelId = await CreatePessoaAsync(client, "Dependente", "Fisica");
        var recebedorFaturaId = await CreatePessoaAsync(client, "Bradesco", "Juridica");
        await CreateFormaPagamentoAsync(client, "Pix recebimento", "Pix", false, false);
        var formaPagamentoCartaoId = await CreateFormaPagamentoAsync(client, "Cartao de credito", "Credito", true, false);
        var cartaoId = await CreateCartaoAsync(client, "Cartao Bradesco", "Visa", "2892", 5, 13);

        var primeiraImportacao = await client.PostAsJsonAsync("/api/v1/importacoes-whatsapp/webhook", new
        {
            tipoOrigem = "Texto",
            remetente = "5511900001111",
            textoBruto = "Compra cartao farmacia popular 89,90"
        });
        primeiraImportacao.EnsureSuccessStatusCode();

        var primeiroDetalhe = await primeiraImportacao.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        primeiroDetalhe.Should().NotBeNull();

        var confirmarPrimeiro = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/itens/{primeiroDetalhe!.Itens.Single().Id}/confirmar",
            new
            {
                observacao = "Historico farmacias",
                descricaoAjustada = "Farmacia do bairro",
                contaGerencialId = contaGerencialDespesaId,
                responsavelId,
                gerarContaReceber = true,
                marcarComoRecorrente = true
            });

        confirmarPrimeiro.EnsureSuccessStatusCode();
        var aprovarPrimeira = await client.PostAsJsonAsync($"/api/v1/importacoes-whatsapp/{primeiroDetalhe.Id}/confirmar", new
        {
            recebedorFaturaId,
            responsavelPagamentoFaturaId = responsavelId,
            cartaoIds = new[] { cartaoId }
        });
        aprovarPrimeira.EnsureSuccessStatusCode();

        var segundaImportacao = await client.PostAsJsonAsync("/api/v1/importacoes-whatsapp/webhook", new
        {
            tipoOrigem = "Texto",
            remetente = "5511900001111",
            textoBruto = "Compra cartao farmacia popular 89,90 novamente"
        });

        segundaImportacao.EnsureSuccessStatusCode();
        var segundoDetalhe = await segundaImportacao.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        segundoDetalhe.Should().NotBeNull();

        var predicao = segundoDetalhe!.Itens.Single().Predicao;
        predicao.Should().NotBeNull();
        predicao!.ContaGerencialId.Should().Be(contaGerencialDespesaId);
        predicao.ResponsavelId.Should().Be(responsavelId);
        predicao.GerarContaReceber.Should().BeTrue();
        predicao.DescricaoAjustada.Should().Be("Farmacia do bairro");
        predicao.MarcarComoRecorrente.Should().BeTrue();
        predicao.QuantidadeOcorrencias.Should().Be(1);
        predicao.ConfiancaHistorico.Should().Be(1m);
        segundoDetalhe.Itens.Single().StatusPrevisaoCodigo.Should().Be("PREVISTO");
        segundoDetalhe.Itens.Single().StatusPrevisaoNome.Should().Be("Previsto");
    }

    [Fact]
    public async Task AprovarImportacaoComCompraCartaoParcelada_DeveGerarFaturasFuturasEMaterializarSomenteContaDaCompetenciaAtual()
    {
        await using var factory = new CustomWebApplicationFactory(services =>
        {
            services.RemoveAll<IImportSuggestionService>();
            services.AddScoped<IImportSuggestionService>(_ =>
                new FixedTypedImportSuggestionService(
                    TipoSugestaoImportacaoWhatsapp.CompraCartao,
                    """
                    {
                      "descricao":"Cadeira ErgoOne 1/3",
                      "valor":300.00,
                      "dataIdentificada":"2026-04-08",
                      "tipoMovimentacaoSugerido":"Saida",
                      "emissor":"NUBANK",
                      "cartaoFinal":"8082",
                      "portador":"Matheus"
                    }
                    """));
        });

        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var contaGerencialId = await CreateContaGerencialAsync(client, "DES.10.02", "Moveis e casa", "Despesa");
        var responsavelId = await CreatePessoaAsync(client, "Matheus", "Fisica");
        var recebedorFaturaId = await CreatePessoaAsync(client, "Nubank", "Juridica");
        var formaPagamentoCartaoId = await CreateFormaPagamentoAsync(client, "Cartao de credito", "Credito", true, false);
        var cartaoId = await CreateCartaoAsync(client, "Cartao Nubank", "Mastercard", "8082", 5, 15);

        var detalhe = await ReceberImportacaoAsync(client, "Compra planejada da cadeira no cartao");
        detalhe.Should().NotBeNull();

        var confirmarResponse = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/itens/{detalhe!.Itens.Single().Id}/confirmar",
            new
            {
                observacao = "Compra parcelada aprovada",
                descricaoAjustada = "Cadeira DT3 ErgoOne 1/3",
                contaGerencialId,
                responsavelId
            });

        confirmarResponse.EnsureSuccessStatusCode();

        var aprovarResponse = await client.PostAsJsonAsync($"/api/v1/importacoes-whatsapp/{detalhe.Id}/confirmar", new
        {
            recebedorFaturaId,
            responsavelPagamentoFaturaId = responsavelId,
            cartaoIds = new[] { cartaoId }
        });

        aprovarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var contasOcultasCartao = dbContext.ContasPagar
            .Where(x => x.OrigemImportacaoWhatsappId == detalhe.Id && x.CartaoId == cartaoId)
            .OrderBy(x => x.NumeroParcela)
            .ToArray();

        contasOcultasCartao.Should().HaveCount(3);
        contasOcultasCartao.Select(x => x.NumeroParcela).Should().Equal(1, 2, 3);
        contasOcultasCartao.Select(x => x.Descricao).Should().Equal(
            "Cadeira DT3 ErgoOne 1/3",
            "Cadeira DT3 ErgoOne 2/3",
            "Cadeira DT3 ErgoOne 3/3");
        contasOcultasCartao.Select(x => x.DataVencimento).Should().Equal(
            new DateOnly(2026, 5, 15),
            new DateOnly(2026, 6, 15),
            new DateOnly(2026, 7, 15));

        var contasFatura = dbContext.ContasPagar
            .Where(x => x.OrigemImportacaoWhatsappId == detalhe.Id && x.FaturaCartaoId.HasValue && !x.CartaoId.HasValue)
            .OrderBy(x => x.NumeroDocumento)
            .ToArray();

        contasFatura.Should().HaveCount(1);
        contasFatura.Single().NumeroDocumento.Should().Be("2026-05");
        contasFatura.Single().ValorLiquido.Should().Be(300m);

        var rateiosPrimeiraFatura = dbContext.RateiosContaGerencial
            .Where(x => x.ContaPagarId == contasFatura[0].Id)
            .ToArray();

        rateiosPrimeiraFatura.Should().ContainSingle();
        rateiosPrimeiraFatura[0].ContaGerencialId.Should().Be(contaGerencialId);
        rateiosPrimeiraFatura[0].Valor.Should().Be(300m);

        var faturas = dbContext.FaturasCartao
            .Where(x => x.CartaoId == cartaoId)
            .OrderBy(x => x.Competencia)
            .ToArray();

        faturas.Select(x => x.Competencia).Should().Equal("2026-05", "2026-06", "2026-07");

        var reabrirResponse = await client.PostAsync($"/api/v1/importacoes-whatsapp/{detalhe.Id}/reabrir", null);
        reabrirResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        dbContext.ContasPagar.Where(x => x.OrigemImportacaoWhatsappId == detalhe.Id).Should().BeEmpty();
        dbContext.FaturasCartao.Where(x => x.CartaoId == cartaoId).Should().BeEmpty();
    }

    [Fact]
    public async Task AprovarImportacaoComParcelaIntermediaria_NaoDeveGerarFaturasDeCompetenciasPassadas()
    {
        await using var factory = new CustomWebApplicationFactory(services =>
        {
            services.RemoveAll<IImportSuggestionService>();
            services.AddScoped<IImportSuggestionService>(_ =>
                new FixedTypedImportSuggestionService(
                    TipoSugestaoImportacaoWhatsapp.CompraCartao,
                    """
                    {
                      "descricao":"ESTADO DE MINAS GERAIS - Parcela 6/8",
                      "valor":507.58,
                      "dataIdentificada":"2026-03-06",
                      "dataVencimento":"2026-04-13",
                      "tipoMovimentacaoSugerido":"Saida",
                      "emissor":"NUBANK",
                      "cartaoFinal":"0101",
                      "portador":"Michelle Ribeiro Macedo",
                      "parcela":"6/8"
                    }
                    """));
        });

        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var contaGerencialId = await CreateContaGerencialAsync(client, "DES.08.02", "Juros multa IOF", "Despesa");
        var responsavelId = await CreatePessoaAsync(client, "Michelle", "Fisica");
        var recebedorFaturaId = await CreatePessoaAsync(client, "Nubank", "Juridica");
        await CreateFormaPagamentoAsync(client, "Cartao de credito", "Credito", true, false);
        var cartaoId = await CreateCartaoAsync(client, "Cartao Nubank", "Mastercard", "0101", 5, 13);

        var detalhe = await ReceberImportacaoAsync(client, "Compra parcelada intermediaria em cartao");
        detalhe.Should().NotBeNull();

        var confirmarResponse = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/itens/{detalhe!.Itens.Single().Id}/confirmar",
            new
            {
                observacao = "Parcela intermediaria",
                descricaoAjustada = "ESTADO DE MINAS GERAIS - Parcela 6/8",
                contaGerencialId,
                responsavelId
            });

        confirmarResponse.EnsureSuccessStatusCode();

        var aprovarResponse = await client.PostAsJsonAsync($"/api/v1/importacoes-whatsapp/{detalhe.Id}/confirmar", new
        {
            recebedorFaturaId,
            responsavelPagamentoFaturaId = responsavelId,
            cartaoIds = new[] { cartaoId }
        });

        aprovarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var contasOcultasCartao = dbContext.ContasPagar
            .Where(x => x.OrigemImportacaoWhatsappId == detalhe.Id && x.CartaoId == cartaoId)
            .OrderBy(x => x.NumeroParcela)
            .ToArray();

        contasOcultasCartao.Should().HaveCount(3);
        contasOcultasCartao.Select(x => x.NumeroParcela).Should().Equal(6, 7, 8);
        contasOcultasCartao.Select(x => x.DataVencimento).Should().Equal(
            new DateOnly(2026, 4, 13),
            new DateOnly(2026, 5, 13),
            new DateOnly(2026, 6, 13));

        var faturas = dbContext.FaturasCartao
            .Where(x => x.CartaoId == cartaoId)
            .OrderBy(x => x.Competencia)
            .ToArray();

        faturas.Select(x => x.Competencia).Should().Equal("2026-04", "2026-05", "2026-06");

        var contasFatura = dbContext.ContasPagar
            .Where(x => x.OrigemImportacaoWhatsappId == detalhe.Id && x.FaturaCartaoId.HasValue && !x.CartaoId.HasValue)
            .OrderBy(x => x.NumeroDocumento)
            .ToArray();

        contasFatura.Should().HaveCount(1);
        contasFatura.Single().NumeroDocumento.Should().Be("2026-04");
    }

    [Fact]
    public async Task AprovarImportacaoBradescoComParcelaAntiga_DeveUsarCompetenciaDaFaturaAtualParaProjetarParcelas()
    {
        await using var factory = new CustomWebApplicationFactory(services =>
        {
            services.RemoveAll<IImportSuggestionService>();
            services.AddScoped<IImportSuggestionService>(_ =>
                new FixedImportSuggestionCollectionService(
                [
                    new ImportSuggestionItem(
                        TipoSugestaoImportacaoWhatsapp.CompraCartao,
                        """
                        {
                          "descricao":"SUPERKIT SUPERMERCADO",
                          "valor":258.55,
                          "dataIdentificada":"2026-03-31",
                          "tipoMovimentacaoSugerido":"Saida",
                          "emissor":"BRADESCO",
                          "cartaoFinal":"2892",
                          "portador":"Michelle Ribeiro Macedo"
                        }
                        """),
                    new ImportSuggestionItem(
                        TipoSugestaoImportacaoWhatsapp.CompraCartao,
                        """
                        {
                          "descricao":"ProdutosUOL 8/12",
                          "valor":10.80,
                          "dataIdentificada":"2025-08-25",
                          "tipoMovimentacaoSugerido":"Saida",
                          "emissor":"BRADESCO",
                          "cartaoFinal":"2892",
                          "portador":"Michelle Ribeiro Macedo",
                          "parcela":"8/12"
                        }
                        """)
                ]));
        });

        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var contaGerencialId = await CreateContaGerencialAsync(client, "DES.02.01", "Supermercado", "Despesa");
        var responsavelId = await CreatePessoaAsync(client, "Michelle", "Fisica");
        var recebedorFaturaId = await CreatePessoaAsync(client, "Bradesco", "Juridica");
        await CreateFormaPagamentoAsync(client, "Cartao de credito", "Credito", true, false);
        var cartaoId = await CreateCartaoAsync(client, "Cartao Bradesco Smiles", "Visa", "2892", 5, 15);

        var detalhe = await ReceberImportacaoAsync(client, "Fatura Bradesco com parcela antiga");
        detalhe.Should().NotBeNull();
        detalhe!.Itens.Should().HaveCount(2);

        foreach (var item in detalhe.Itens)
        {
            var confirmarResponse = await client.PostAsJsonAsync(
                $"/api/v1/importacoes-whatsapp/itens/{item.Id}/confirmar",
                new
                {
                    observacao = "Item aprovado",
                    contaGerencialId,
                    responsavelId
                });

            confirmarResponse.EnsureSuccessStatusCode();
        }

        var aprovarResponse = await client.PostAsJsonAsync($"/api/v1/importacoes-whatsapp/{detalhe.Id}/confirmar", new
        {
            recebedorFaturaId,
            responsavelPagamentoFaturaId = responsavelId,
            cartaoIds = new[] { cartaoId }
        });

        aprovarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var faturas = dbContext.FaturasCartao
            .Where(x => x.CartaoId == cartaoId)
            .OrderBy(x => x.Competencia)
            .ToArray();

        faturas.Select(x => x.Competencia).Should().Equal("2026-04", "2026-05", "2026-06", "2026-07", "2026-08");

        var contasParceladas = dbContext.ContasPagar
            .Where(x => x.OrigemImportacaoWhatsappId == detalhe.Id && x.CartaoId == cartaoId && x.QuantidadeParcelas == 12)
            .OrderBy(x => x.NumeroParcela)
            .ToArray();

        contasParceladas.Select(x => x.NumeroParcela).Should().Equal(8, 9, 10, 11, 12);
        contasParceladas.Select(x => x.DataVencimento).Should().Equal(
            new DateOnly(2026, 4, 15),
            new DateOnly(2026, 5, 15),
            new DateOnly(2026, 6, 15),
            new DateOnly(2026, 7, 15),
            new DateOnly(2026, 8, 15));
    }

    [Fact]
    public async Task CompletarFechamentoFatura_QuandoImportacaoJaEstiverConfirmada_DeveMaterializarSemDuplicar()
    {
        await using var factory = new CustomWebApplicationFactory(services =>
        {
            services.RemoveAll<IImportSuggestionService>();
            services.AddScoped<IImportSuggestionService>(_ =>
                new FixedTypedImportSuggestionService(
                    TipoSugestaoImportacaoWhatsapp.CompraCartao,
                    """
                    {
                      "descricao":"Cadeira ErgoOne 1/3",
                      "valor":300.00,
                      "dataIdentificada":"2026-04-08",
                      "tipoMovimentacaoSugerido":"Saida",
                      "emissor":"NUBANK",
                      "cartaoFinal":"8082",
                      "portador":"Matheus"
                    }
                    """));
        });

        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var contaGerencialId = await CreateContaGerencialAsync(client, "DES.10.02", "Moveis e casa", "Despesa");
        var responsavelId = await CreatePessoaAsync(client, "Matheus", "Fisica");
        var recebedorFaturaId = await CreatePessoaAsync(client, "Nubank", "Juridica");
        var formaPagamentoCartaoId = await CreateFormaPagamentoAsync(client, "Cartao de credito", "Credito", true, false);
        var cartaoId = await CreateCartaoAsync(client, "Cartao Nubank", "Mastercard", "8082", 5, 15);

        var detalhe = await ReceberImportacaoAsync(client, "Compra aprovada antes do fechamento da fatura");
        detalhe.Should().NotBeNull();

        var confirmarResponse = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/itens/{detalhe!.Itens.Single().Id}/confirmar",
            new
            {
                observacao = "Compra parcelada aprovada",
                descricaoAjustada = "Cadeira DT3 ErgoOne 1/3",
                contaGerencialId,
                responsavelId
            });

        confirmarResponse.EnsureSuccessStatusCode();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var importacao = dbContext.ImportacoesWhatsapp
                .Include(x => x.Itens)
                .Single(x => x.Id == detalhe.Id);
            importacao.AprovarRevisao();
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var completarResponse = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/{detalhe.Id}/completar-fechamento-fatura",
            new
            {
                recebedorFaturaId,
                responsavelPagamentoFaturaId = responsavelId,
                cartaoIds = new[] { cartaoId }
            });

        completarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detalheComFechamento = await completarResponse.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        detalheComFechamento.Should().NotBeNull();
        detalheComFechamento!.PossuiGeracaoFinanceira.Should().BeTrue();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            dbContext.ContasPagar.Count(x => x.OrigemImportacaoWhatsappId == detalhe.Id && x.CartaoId == cartaoId)
                .Should().Be(3);
            dbContext.ContasPagar.Count(x => x.OrigemImportacaoWhatsappId == detalhe.Id && x.FaturaCartaoId.HasValue && !x.CartaoId.HasValue)
                .Should().Be(1);
            dbContext.FaturasCartao.Count(x => x.CartaoId == cartaoId)
                .Should().Be(3);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            dbContext.DefinirFamiliaCorrente(FamiliaDesenvolvimentoId);
            var faturaCartaoAppService = scope.ServiceProvider.GetRequiredService<FaturaCartaoAppService>();

            var contaLegada = ContaPagar.Criar(
                numeroDocumento: null,
                dataEmissao: new DateOnly(2025, 8, 25),
                responsavelCompraId: responsavelId,
                recebedorId: recebedorFaturaId,
                dataVencimento: new DateOnly(2025, 9, 15),
                formaPagamentoId: formaPagamentoCartaoId,
                cartaoId: cartaoId,
                contaBancariaId: null,
                valorOriginal: 300m,
                valorDesconto: 0m,
                valorJuros: 0m,
                valorMulta: 0m,
                quantidadeParcelas: 1,
                numeroParcela: 1,
                grupoParcelamentoId: null,
                origemCompraPlanejadaId: null,
                descricao: "Compra legada incorreta",
                observacao: "Conta simulada para validar rematerialização do fechamento.",
                statusContaId: StatusConta.PendenteId,
                ehRecorrente: false,
                regraRecorrenciaId: null,
                origem: OrigemLancamento.Importacao,
                rateios:
                [
                    RateioPlano.CreateSigned(contaGerencialId, 300m)
                ]);

            contaLegada.VincularOrigemImportacao(detalhe.Id);
            contaLegada.DefinirChaveSerieImportacaoCartao("legacy-wrong-competencia");

            dbContext.ContasPagar.Add(contaLegada);
            dbContext.RateiosContaGerencial.AddRange(contaLegada.Rateios);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            await faturaCartaoAppService.SincronizarAsync(CancellationToken.None);

            dbContext.ContasPagar.Count(x => x.OrigemImportacaoWhatsappId == detalhe.Id && x.CartaoId == cartaoId)
                .Should().Be(4);
            dbContext.FaturasCartao.Count(x => x.CartaoId == cartaoId)
                .Should().Be(4);
        }

        var repetirResponse = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/{detalhe.Id}/completar-fechamento-fatura",
            new
            {
                recebedorFaturaId,
                responsavelPagamentoFaturaId = responsavelId,
                cartaoIds = new[] { cartaoId }
            });

        repetirResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            dbContext.ContasPagar.Count(x => x.OrigemImportacaoWhatsappId == detalhe.Id && x.CartaoId == cartaoId)
                .Should().Be(3);
            dbContext.ContasPagar.Count(x => x.OrigemImportacaoWhatsappId == detalhe.Id && x.FaturaCartaoId.HasValue && !x.CartaoId.HasValue)
                .Should().Be(1);
            dbContext.FaturasCartao.Count(x => x.CartaoId == cartaoId)
                .Should().Be(3);
        }
    }

    [Fact]
    public async Task CompletarFechamentoFatura_QuandoHouverEstorno_DeveAbaterValorDaFaturaSemBloquearFechamento()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var contaGerencialId = await CreateContaGerencialAsync(client, "DES.03.02", "App, Uber e onibus", "Despesa");
        var responsavelId = await CreatePessoaAsync(client, "Michelle", "Fisica");
        var recebedorFaturaId = await CreatePessoaAsync(client, "Nubank", "Juridica");
        await CreateFormaPagamentoAsync(client, "Cartao de credito", "Credito", true, false);
        var cartaoId = await CreateCartaoAsync(client, "Cartao Nubank", "Mastercard", "0101", 5, 13);

        Guid importacaoId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            dbContext.DefinirFamiliaCorrente(FamiliaDesenvolvimentoId);

            var importacao = ImportacaoWhatsapp.CriarRecebida(
                TipoOrigemImportacaoWhatsapp.Arquivo,
                "nubank-modelo",
                null,
                "Nubank_2026-04-13.pdf",
                "bucket://nubank.pdf",
                "application/pdf");

            importacao.RegistrarExtracaoComSucesso(0.93m);

            var itemCompra = ItemImportadoWhatsapp.Criar(
                importacao.Id,
                TipoSugestaoImportacaoWhatsapp.CompraCartao,
                """
                {
                  "descricao":"Uber - NuPay",
                  "valor":8.96,
                  "dataIdentificada":"2026-03-11",
                  "dataVencimento":"2026-04-13",
                  "tipoMovimentacaoSugerido":"Saida",
                  "emissor":"NUBANK",
                  "cartaoFinal":"0101",
                  "portador":"Michelle Ribeiro Macedo"
                }
                """,
                "UBERNUPAY");
            itemCompra.Confirmar(null, null, contaGerencialId, responsavelId, null, false);

            var itemEstorno = ItemImportadoWhatsapp.Criar(
                importacao.Id,
                TipoSugestaoImportacaoWhatsapp.CompraCartao,
                """
                {
                  "descricao":"Estorno de Uber - NuPay",
                  "valor":-12.96,
                  "dataIdentificada":"2026-03-14",
                  "dataVencimento":"2026-04-13",
                  "tipoMovimentacaoSugerido":"Entrada",
                  "emissor":"NUBANK",
                  "cartaoFinal":"0101",
                  "portador":"Michelle Ribeiro Macedo"
                }
                """,
                "ESTORNOUBER");
            itemEstorno.Confirmar(null, "Estorno de Uber - NuPay", contaGerencialId, responsavelId, null, false);

            var itemCompraMaior = ItemImportadoWhatsapp.Criar(
                importacao.Id,
                TipoSugestaoImportacaoWhatsapp.CompraCartao,
                """
                {
                  "descricao":"Skalla",
                  "valor":60.00,
                  "dataIdentificada":"2026-03-10",
                  "dataVencimento":"2026-04-13",
                  "tipoMovimentacaoSugerido":"Saida",
                  "emissor":"NUBANK",
                  "cartaoFinal":"0101",
                  "portador":"Michelle Ribeiro Macedo"
                }
                """,
                "SKALLA");
            itemCompraMaior.Confirmar(null, null, contaGerencialId, responsavelId, null, false);

            importacao.SubstituirItens([itemCompra, itemEstorno, itemCompraMaior]);
            importacao.AprovarRevisao();

            dbContext.ImportacoesWhatsapp.Add(importacao);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            importacaoId = importacao.Id;
        }

        var completarResponse = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/{importacaoId}/completar-fechamento-fatura",
            new
            {
                recebedorFaturaId,
                responsavelPagamentoFaturaId = responsavelId,
                cartaoIds = new[] { cartaoId }
            });

        completarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            var contasCartao = dbContext.ContasPagar
                .Where(x => x.OrigemImportacaoWhatsappId == importacaoId && x.CartaoId == cartaoId)
                .OrderBy(x => x.DataEmissao)
                .ToArray();

            contasCartao.Should().HaveCount(3);
            contasCartao.Should().ContainSingle(x => x.ValorLiquido == -12.96m);

            var contaFatura = dbContext.ContasPagar
                .Single(x => x.OrigemImportacaoWhatsappId == importacaoId && x.FaturaCartaoId.HasValue && !x.CartaoId.HasValue);

            var rateiosFatura = dbContext.RateiosContaGerencial
                .Where(x => x.ContaPagarId == contaFatura.Id)
                .ToArray();

            contaFatura.ValorLiquido.Should().Be(56.00m);
            rateiosFatura.Should().ContainSingle();
            rateiosFatura.Single().Valor.Should().Be(56.00m);
        }
    }

    [Fact]
    public async Task GetDetalhe_QuandoNaoHouverHistorico_MasDescricaoForObvia_DeveSugerirContaGerencialEPadraoResponsavel()
    {
        await using var factory = new CustomWebApplicationFactory(services =>
        {
            services.RemoveAll<IImportSuggestionService>();
            services.AddScoped<IImportSuggestionService>(_ =>
                new FixedImportSuggestionService("""
                {
                  "descricao":"Superkit Supermercado",
                  "valor":86.85,
                  "dataIdentificada":"2026-04-03",
                  "dataVencimento":"2026-04-13",
                  "tipoMovimentacaoSugerido":"Saida",
                  "emissor":"NUBANK",
                  "cartaoFinal":"8082",
                  "portador":"Matheus Ferreira"
                }
                """));
        });

        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var responsavelPadraoId = await CreatePessoaAsync(client, "Matheus", "Fisica");
        var contaGerencialDespesaId = await CreateContaGerencialAsync(
            client,
            "DES.02.01",
            "Supermercado",
            "Despesa",
            false,
            responsavelPadraoId);

        var resposta = await client.PostAsJsonAsync("/api/v1/importacoes-whatsapp/webhook", new
        {
            tipoOrigem = "Texto",
            remetente = "5511900001111",
            textoBruto = "Compra cartao superkit supermercado 86,85"
        });

        resposta.EnsureSuccessStatusCode();
        var detalhe = await resposta.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();

        detalhe.Should().NotBeNull();
        var predicao = detalhe!.Itens.Single().Predicao;
        predicao.Should().NotBeNull();
        predicao!.ContaGerencialId.Should().Be(contaGerencialDespesaId);
        predicao.ResponsavelId.Should().Be(responsavelPadraoId);
        predicao.DescricaoAjustada.Should().BeNull();
        predicao.GerarContaReceber.Should().BeFalse();
        predicao.MarcarComoRecorrente.Should().BeFalse();
        predicao.QuantidadeOcorrencias.Should().Be(0);
        predicao.ConfiancaHistorico.Should().Be(0.67m);
    }

    [Fact]
    public async Task AprovarImportacaoComCompraCartaoRecorrente_DeveGerarSomenteFaturaAtualEFuturas()
    {
        await using var factory = new CustomWebApplicationFactory(services =>
        {
            services.RemoveAll<IImportSuggestionService>();
            services.AddScoped<IImportSuggestionService>(_ =>
                new FixedTypedImportSuggestionService(
                    TipoSugestaoImportacaoWhatsapp.CompraCartao,
                    """
                    {
                      "descricao":"Spotify",
                      "valor":21.90,
                      "dataIdentificada":"2026-03-28",
                      "dataVencimento":"2026-04-13",
                      "tipoMovimentacaoSugerido":"Saida",
                      "emissor":"NUBANK",
                      "cartaoFinal":"0101",
                      "portador":"Michelle Ribeiro Macedo"
                    }
                    """));
        });

        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var contaGerencialId = await CreateContaGerencialAsync(client, "DES.06.02", "Streaming e entretenimento", "Despesa");
        var responsavelId = await CreatePessoaAsync(client, "Michelle", "Fisica");
        var recebedorFaturaId = await CreatePessoaAsync(client, "Nubank", "Juridica");
        await CreateFormaPagamentoAsync(client, "Cartao de credito", "Credito", true, false);
        var cartaoId = await CreateCartaoAsync(client, "Cartao Nubank", "Mastercard", "0101", 5, 13);

        var detalhe = await ReceberImportacaoAsync(client, "Compra recorrente spotify");
        detalhe.Should().NotBeNull();

        var confirmarResponse = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/itens/{detalhe!.Itens.Single().Id}/confirmar",
            new
            {
                observacao = "Assinatura recorrente",
                contaGerencialId,
                responsavelId,
                marcarComoRecorrente = true
            });

        confirmarResponse.EnsureSuccessStatusCode();

        var aprovarResponse = await client.PostAsJsonAsync($"/api/v1/importacoes-whatsapp/{detalhe.Id}/confirmar", new
        {
            recebedorFaturaId,
            responsavelPagamentoFaturaId = responsavelId,
            cartaoIds = new[] { cartaoId }
        });

        aprovarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var faturas = dbContext.FaturasCartao
            .Where(x => x.CartaoId == cartaoId)
            .OrderBy(x => x.Competencia)
            .Select(x => x.Competencia)
            .Take(4)
            .ToArray();

        faturas.Should().Equal("2026-04", "2026-05", "2026-06", "2026-07");
        dbContext.FaturasCartao.Should().NotContain(x => x.CartaoId == cartaoId && string.CompareOrdinal(x.Competencia, "2026-04") < 0);
    }

    [Fact]
    public async Task GetDetalhe_QuandoDescricaoTiverPosto_DevePriorizarCombustivelMesmoComNomeAmbiguo()
    {
        await using var factory = new CustomWebApplicationFactory(services =>
        {
            services.RemoveAll<IImportSuggestionService>();
            services.AddScoped<IImportSuggestionService>(_ =>
                new FixedImportSuggestionService("""
                {
                  "descricao":"Auto Posto Skalla",
                  "valor":120.00,
                  "dataIdentificada":"2026-04-03",
                  "dataVencimento":"2026-04-13",
                  "tipoMovimentacaoSugerido":"Saida",
                  "emissor":"NUBANK",
                  "cartaoFinal":"8082",
                  "portador":"Matheus Ferreira"
                }
                """));
        });

        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var responsavelPadraoId = await CreatePessoaAsync(client, "Matheus", "Fisica");
        var contaCombustivelId = await CreateContaGerencialAsync(
            client,
            "DES.03.01",
            "Combustível",
            "Despesa",
            false,
            responsavelPadraoId);
        await CreateContaGerencialAsync(client, "DES.02.02", "Restaurantes", "Despesa");

        var resposta = await client.PostAsJsonAsync("/api/v1/importacoes-whatsapp/webhook", new
        {
            tipoOrigem = "Texto",
            remetente = "5511900002222",
            textoBruto = "Compra cartao auto posto skalla 120,00"
        });

        resposta.EnsureSuccessStatusCode();
        var detalhe = await resposta.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();

        detalhe.Should().NotBeNull();
        var predicao = detalhe!.Itens.Single().Predicao;
        predicao.Should().NotBeNull();
        predicao!.ContaGerencialId.Should().Be(contaCombustivelId);
        predicao.ResponsavelId.Should().Be(responsavelPadraoId);
    }

    private static async Task<ImportacaoWhatsappDetalheResponse?> ReceberImportacaoAsync(HttpClient client, string textoBruto)
    {
        var webhookResponse = await client.PostAsJsonAsync("/api/v1/importacoes-whatsapp/webhook", new
        {
            tipoOrigem = "Texto",
            remetente = "5511900011111",
            textoBruto
        });

        webhookResponse.EnsureSuccessStatusCode();
        return await webhookResponse.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
    }

    private static async Task<Guid> CreatePessoaAsync(HttpClient client, string nome, string tipoPessoa)
    {
        var response = await client.PostAsJsonAsync("/api/v1/pessoas", new
        {
            nome,
            tipoPessoa
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private static async Task<Guid> CreateFormaPagamentoAsync(
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

    private static async Task<Guid> CreateCartaoAsync(
        HttpClient client,
        string nome,
        string bandeira,
        string numeroFinal,
        int diaFechamentoFatura,
        int diaVencimentoFatura)
    {
        var response = await client.PostAsJsonAsync("/api/v1/cartoes", new
        {
            nome,
            bandeira,
            numeroFinal,
            diaFechamentoFatura,
            diaVencimentoFatura,
            ativo = true
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private static async Task<Guid> CreateContaGerencialAsync(
        HttpClient client,
        string codigo,
        string descricao,
        string tipo,
        bool ehPadraoRecebimentoFaturaCartao = false,
        Guid? responsavelPadraoId = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo,
            descricao,
            tipo,
            contaPaiId = (Guid?)null,
            responsavelPadraoId,
            ativo = true,
            ehPadraoRecebimentoFaturaCartao
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

    private sealed record IdResponse(Guid Id);

    private sealed record ApiErrorResponse(
        string Code,
        string Message,
        IReadOnlyDictionary<string, string[]> Errors,
        string TraceId);

    private sealed record ImportacaoWhatsappResumoResponse(
        Guid Id,
        string TipoOrigemCodigo,
        string TipoOrigemNome,
        string Remetente,
        string? TextoBruto,
        string? NomeArquivo,
        string? MimeType,
        string StatusCodigo,
        string StatusNome,
        decimal? ConfiancaExtracao,
        int QuantidadeItens,
        int QuantidadePendentes,
        DateTime RecebidoEmUtc,
        DateTime? ProcessadoEmUtc);

    private sealed record ImportacaoWhatsappDetalheResponse(
        Guid Id,
        string TipoOrigemCodigo,
        string TipoOrigemNome,
        string Remetente,
        string? TextoBruto,
        string? NomeArquivo,
        string? CaminhoArquivo,
        string? MimeType,
        string StatusCodigo,
        string StatusNome,
        decimal? ConfiancaExtracao,
        string? MensagemErro,
        DateTime RecebidoEmUtc,
        DateTime? ProcessadoEmUtc,
        DateTime? ConfirmadoEmUtc,
        DateTime? RejeitadoEmUtc,
        bool PossuiGeracaoFinanceira,
        IReadOnlyCollection<ItemImportadoWhatsappResponse> Itens);

    private sealed record ItemImportadoWhatsappResponse(
        Guid Id,
        Guid ImportacaoWhatsappId,
        string TipoSugestaoCodigo,
        string TipoSugestaoNome,
        string PayloadSugeridoJson,
        string StatusCodigo,
        string StatusNome,
        string? DescricaoAjustada,
        bool MarcarComoRecorrente,
        Guid? ContaGerencialId,
        string? ContaGerencialDescricao,
        Guid? ResponsavelId,
        string? ResponsavelNome,
        Guid? ContaReceberId,
        Guid? MovimentacaoFinanceiraId,
        string? StatusPrevisaoCodigo,
        string? StatusPrevisaoNome,
        string? Observacao,
        DateTime? ConfirmadoEmUtc,
        DateTime? RejeitadoEmUtc,
        PredicaoClassificacaoImportacaoWhatsappResponse? Predicao);

    private sealed record PredicaoClassificacaoImportacaoWhatsappResponse(
        Guid? ContaGerencialId,
        string? ContaGerencialDescricao,
        Guid? ResponsavelId,
        string? ResponsavelNome,
        string? DescricaoAjustada,
        bool GerarContaReceber,
        bool MarcarComoRecorrente,
        int QuantidadeOcorrencias,
        decimal ConfiancaHistorico);

    private sealed class ThrowingDocumentExtractor : IDocumentExtractor
    {
        public Task<DocumentExtractionResult> ExtractAsync(DocumentExtractionRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Extrator indisponivel.");
        }
    }

    private sealed class FixedImportSuggestionService(string payloadJson) : IImportSuggestionService
    {
        public Task<IReadOnlyCollection<ImportSuggestionItem>> GenerateAsync(
            ImportSuggestionRequest request,
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<ImportSuggestionItem> items =
            [
                new(TipoSugestaoImportacaoWhatsapp.CompraCartao, payloadJson)
            ];

            return Task.FromResult(items);
        }
    }

    private sealed class FixedTypedImportSuggestionService(
        TipoSugestaoImportacaoWhatsapp tipoSugestao,
        string payloadJson) : IImportSuggestionService
    {
        public Task<IReadOnlyCollection<ImportSuggestionItem>> GenerateAsync(
            ImportSuggestionRequest request,
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<ImportSuggestionItem> items =
            [
                new(tipoSugestao, payloadJson)
            ];

            return Task.FromResult(items);
        }
    }

    private sealed class FixedImportSuggestionCollectionService(
        IReadOnlyCollection<ImportSuggestionItem> items) : IImportSuggestionService
    {
        public Task<IReadOnlyCollection<ImportSuggestionItem>> GenerateAsync(
            ImportSuggestionRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(items);
        }
    }

    private sealed class SequencedImportSuggestionService(
        IReadOnlyList<ImportSuggestionItem> sequencia) : IImportSuggestionService
    {
        private int _index = -1;

        public Task<IReadOnlyCollection<ImportSuggestionItem>> GenerateAsync(
            ImportSuggestionRequest request,
            CancellationToken cancellationToken)
        {
            var nextIndex = Interlocked.Increment(ref _index);
            var item = sequencia[Math.Min(nextIndex, sequencia.Count - 1)];
            IReadOnlyCollection<ImportSuggestionItem> items = [item];
            return Task.FromResult(items);
        }
    }

    private static byte[] CreatePlainTextPseudoPdf(IReadOnlyCollection<string> lines)
    {
        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 12 Tf");
        content.AppendLine("1 0 0 1 0 0 Tm");

        foreach (var line in lines)
        {
            content.Append('(')
                .Append(EscapePdfLiteral(line))
                .AppendLine(") Tj");
        }

        content.AppendLine("ET");

        return Encoding.Latin1.GetBytes(
            """
            %PDF-1.4
            1 0 obj
            << /Type /Catalog >>
            endobj
            2 0 obj
            << /Length 0 >>
            stream
            """ + content + """
            endstream
            endobj
            """);
    }

    private static string EscapePdfLiteral(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }
}
