using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Importacoes;

public sealed class ImportacoesWhatsappControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
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
    public async Task ConfirmarRejeitarEReprocessar_DeveAtualizarItensEStatusDaImportacao()
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
        detalheConfirmado!.StatusCodigo.Should().Be("CONFIRMADO");
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
        detalheRejeitado!.StatusCodigo.Should().Be("REJEITADO");
        detalheRejeitado.Itens.Should().ContainSingle(item => item.StatusCodigo == "REJEITADO");

        var reprocessarResponse = await client.PostAsync($"/api/v1/importacoes-whatsapp/{detalheRejeicao.Id}/reprocessar", null);

        reprocessarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detalheReprocessado = await reprocessarResponse.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        detalheReprocessado.Should().NotBeNull();
        detalheReprocessado!.StatusCodigo.Should().Be("PENDENTE_REVISAO");
        detalheReprocessado.Itens.Should().ContainSingle(item => item.StatusCodigo == "SUGERIDO");
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

    private sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

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
        IReadOnlyCollection<ItemImportadoWhatsappResponse> Itens);

    private sealed record ItemImportadoWhatsappResponse(
        Guid Id,
        Guid ImportacaoWhatsappId,
        string TipoSugestaoCodigo,
        string TipoSugestaoNome,
        string PayloadSugeridoJson,
        string StatusCodigo,
        string StatusNome,
        string? Observacao,
        DateTime? ConfirmadoEmUtc,
        DateTime? RejeitadoEmUtc);
}
