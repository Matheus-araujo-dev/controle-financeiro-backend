using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Api.Tests.Conciliacao;

public sealed class ConciliacaoControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task GetEPostConfirmarVinculo_DeveProporCandidatoEConciliarMovimentacaoComAuditoria()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await Financeiro.FinancialFixtureSeed.CreateAsync(client);
        var movimentacaoId = await CriarContaReceberLiquidadaAsync(client, fixture);
        var itemExtratoId = await CriarEConfirmarItemExtratoAsync(client);

        var conciliacao = await client.GetFromJsonAsync<PagedResponse<ConciliacaoItemResponse>>("/api/v1/conciliacao");

        conciliacao.Should().NotBeNull();
        conciliacao!.Items.Should().ContainSingle();

        var item = conciliacao.Items.Single();
        item.ItemImportadoWhatsappId.Should().Be(itemExtratoId);
        item.StatusConciliacaoCodigo.Should().Be("PENDENTE");
        item.Candidatas.Should().Contain(candidate =>
            candidate.MovimentacaoFinanceiraId == movimentacaoId &&
            candidate.Valor == 80m);

        var confirmarResponse = await client.PostAsJsonAsync($"/api/v1/conciliacao/{itemExtratoId}/confirmar-vinculo", new
        {
            movimentacaoFinanceiraId = movimentacaoId,
            observacao = "Conciliacao manual do extrato"
        });

        confirmarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var itemConciliado = await confirmarResponse.Content.ReadFromJsonAsync<ConciliacaoItemResponse>();
        itemConciliado.Should().NotBeNull();
        itemConciliado!.StatusConciliacaoCodigo.Should().Be("CONCILIADO");
        itemConciliado.MovimentacaoConciliadaId.Should().Be(movimentacaoId);

        var movimentacao = await client.GetFromJsonAsync<MovimentacaoDetalheResponse>($"/api/v1/movimentacoes/{movimentacaoId}");
        movimentacao.Should().NotBeNull();
        movimentacao!.StatusCodigo.Should().Be("CONCILIADA");
        movimentacao.DataConciliacao.Should().NotBeNull();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditEntries = await dbContext.AuditTrailEntries
            .Where(entry =>
                entry.EntityName == "MovimentacaoFinanceira" ||
                entry.EntityName == "ItemImportadoWhatsapp")
            .ToListAsync();

        auditEntries.Should().Contain(entry => entry.EntityName == "MovimentacaoFinanceira" && entry.Action == "Updated");
        auditEntries.Should().Contain(entry => entry.EntityName == "ItemImportadoWhatsapp" && entry.Action == "Updated");
    }

    [Fact]
    public async Task PostConfirmarVinculo_QuandoItemNaoForExtratoConfirmado_DeveRetornarBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var webhookResponse = await client.PostAsJsonAsync("/api/v1/importacoes-whatsapp/webhook", new
        {
            tipoOrigem = "Texto",
            remetente = "5511933332222",
            textoBruto = "Pagar academia 120,50"
        });

        var detalheImportacao = await webhookResponse.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        detalheImportacao.Should().NotBeNull();

        var response = await client.PostAsJsonAsync($"/api/v1/conciliacao/{detalheImportacao!.Itens.Single().Id}/confirmar-vinculo", new
        {
            movimentacaoFinanceiraId = Guid.NewGuid()
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<Guid> CriarContaReceberLiquidadaAsync(
        HttpClient client,
        Financeiro.FinancialFixtureSeed.FixtureIds fixture)
    {
        var createResponse = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            numeroDocumento = "REC-01",
            dataEmissao = "2026-04-08",
            responsavelId = fixture.ResponsavelId,
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-08",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            cartaoId = (string?)null,
            contaBancariaId = (string?)null,
            valorOriginal = 80m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Recebimento conciliacao",
            observacao = "Receita para teste de conciliacao",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 80m }
            }
        });

        createResponse.EnsureSuccessStatusCode();
        var conta = await createResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        var liquidarResponse = await client.PostAsJsonAsync($"/api/v1/contas-receber/{conta!.Id}/liquidar", new
        {
            dataLiquidacao = "2026-04-08",
            contaBancariaId = fixture.ContaBancariaId
        });

        liquidarResponse.EnsureSuccessStatusCode();

        var movimentacoes = await client.GetFromJsonAsync<PagedResponse<MovimentacaoResumoResponse>>("/api/v1/movimentacoes?search=conciliacao");
        return movimentacoes!.Items.Single(item => item.ContaReceberId == conta.Id).Id;
    }

    private static async Task<Guid> CriarEConfirmarItemExtratoAsync(HttpClient client)
    {
        var webhookResponse = await client.PostAsJsonAsync("/api/v1/importacoes-whatsapp/webhook", new
        {
            tipoOrigem = "Texto",
            remetente = "5511944443333",
            textoBruto = "Extrato pix recebido cliente 80,00 2026-04-08"
        });

        webhookResponse.EnsureSuccessStatusCode();
        var detalheImportacao = await webhookResponse.Content.ReadFromJsonAsync<ImportacaoWhatsappDetalheResponse>();
        var itemExtrato = detalheImportacao!.Itens.Single();

        var confirmarItemResponse = await client.PostAsJsonAsync(
            $"/api/v1/importacoes-whatsapp/itens/{itemExtrato.Id}/confirmar",
            new
            {
                observacao = "Extrato validado para conciliacao"
            });

        confirmarItemResponse.EnsureSuccessStatusCode();
        return itemExtrato.Id;
    }

    private sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

    private sealed record ConciliacaoItemResponse(
        Guid ItemImportadoWhatsappId,
        Guid ImportacaoWhatsappId,
        string Remetente,
        string? DescricaoExtrato,
        decimal? ValorSugerido,
        DateOnly? DataSugerida,
        string StatusConciliacaoCodigo,
        string StatusConciliacaoNome,
        Guid? MovimentacaoConciliadaId,
        string? MovimentacaoConciliadaDescricao,
        IReadOnlyCollection<ConciliacaoCandidataResponse> Candidatas);

    private sealed record ConciliacaoCandidataResponse(
        Guid MovimentacaoFinanceiraId,
        DateOnly DataMovimentacao,
        string Tipo,
        string Natureza,
        decimal Valor,
        string StatusCodigo,
        string? Observacao,
        int Score);

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

    private sealed record ContaDetalheResponse(Guid Id);

    private sealed record MovimentacaoResumoResponse(
        Guid Id,
        DateOnly DataMovimentacao,
        string Tipo,
        string Natureza,
        string StatusCodigo,
        string StatusNome,
        decimal Valor,
        Guid? ContaBancariaId,
        string? ContaBancariaNome,
        Guid? ContaPagarId,
        Guid? ContaReceberId,
        Guid? FaturaCartaoId,
        string? Observacao);

    private sealed record MovimentacaoDetalheResponse(
        Guid Id,
        DateOnly DataMovimentacao,
        string Tipo,
        string Natureza,
        string StatusCodigo,
        string StatusNome,
        decimal Valor,
        Guid? ContaBancariaId,
        string? ContaBancariaNome,
        Guid? ContaPagarId,
        Guid? ContaReceberId,
        Guid? FaturaCartaoId,
        string? Observacao,
        DateOnly? DataConciliacao);
}
