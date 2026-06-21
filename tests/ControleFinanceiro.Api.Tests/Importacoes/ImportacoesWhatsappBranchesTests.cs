using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Importacoes;

// Cobre branches ainda descobertos do ImportacaoWhatsappCommandService: guardas de não-encontrado (404)
// de cada método de mutação e o caminho de webhook por imagem (extração via vision fake).
public sealed class ImportacoesWhatsappBranchesTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static readonly object CorpoAprovar = new { recebedorFaturaId = (Guid?)null, responsavelPagamentoFaturaId = (Guid?)null, cartaoIds = (Guid[]?)null };
    private static readonly object CorpoRevisar = new
    {
        observacao = (string?)null,
        descricaoAjustada = (string?)null,
        contaGerencialId = (Guid?)null,
        responsavelId = (Guid?)null,
        dataVencimentoContaReceber = (DateOnly?)null,
        gerarContaReceber = false,
        marcarComoRecorrente = false
    };

    [Fact]
    public async Task Reprocessar_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var r = await client.PostAsync($"/api/v1/importacoes-whatsapp/{Guid.NewGuid()}/reprocessar", content: null);
        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reabrir_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var r = await client.PostAsync($"/api/v1/importacoes-whatsapp/{Guid.NewGuid()}/reabrir", content: null);
        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Confirmar_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var r = await client.PostAsJsonAsync($"/api/v1/importacoes-whatsapp/{Guid.NewGuid()}/confirmar", CorpoAprovar);
        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CompletarFechamentoFatura_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var r = await client.PostAsJsonAsync($"/api/v1/importacoes-whatsapp/{Guid.NewGuid()}/completar-fechamento-fatura", CorpoAprovar);
        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConfirmarItem_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var r = await client.PostAsJsonAsync($"/api/v1/importacoes-whatsapp/itens/{Guid.NewGuid()}/confirmar", CorpoRevisar);
        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RejeitarItem_Inexistente_DeveRetornar404()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var r = await client.PostAsJsonAsync($"/api/v1/importacoes-whatsapp/itens/{Guid.NewGuid()}/rejeitar", CorpoRevisar);
        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Webhook_ComImagem_DeveProcessarViaVision()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var resposta = await client.PostAsJsonAsync("/api/v1/importacoes-whatsapp/webhook", new
        {
            tipoOrigem = "Imagem",
            remetente = "5511990001122",
            textoBruto = (string?)null,
            nomeArquivo = "recibo.jpg",
            mimeType = "image/jpeg",
            arquivoBase64 = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8])
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
