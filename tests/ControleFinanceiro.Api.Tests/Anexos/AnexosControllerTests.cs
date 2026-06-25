using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Anexos;
using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Api.Tests.Anexos;

public sealed class AnexosControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task UploadListagemDownloadEExclusao_DevemFuncionarNosQuatroTiposDeRegistro()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var contaPagarId = await CriarContaPagarAsync(client, fixture);
        var contaReceberId = await CriarContaReceberAsync(client, fixture);
        var compraPlanejadaId = await CriarCompraPlanejadaAsync(client, fixture);
        var faturaId = await CriarFaturaAsync(fixture.CartaoId);

        var alvos = new[]
        {
            (Tipo: "contas-pagar", Id: contaPagarId),
            (Tipo: "contas-receber", Id: contaReceberId),
            (Tipo: "faturas", Id: faturaId),
            (Tipo: "compras-planejadas", Id: compraPlanejadaId)
        };

        foreach (var alvo in alvos)
        {
            var response = await UploadPdfAsync(client, alvo.Tipo, alvo.Id, $"comprovante-{alvo.Tipo}.pdf");
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var segundo = await UploadPdfAsync(client, "contas-pagar", contaPagarId, "nota-fiscal.pdf");
        var segundoAnexo = await segundo.Content.ReadFromJsonAsync<AnexoResponse>();
        var anexosConta = await client.GetFromJsonAsync<IReadOnlyCollection<AnexoResponse>>(
            $"/api/v1/anexos/contas-pagar/{contaPagarId}");

        anexosConta.Should().HaveCount(2);
        anexosConta.Should().Contain(x => x.NomeArquivo == "nota-fiscal.pdf");

        var download = await client.GetAsync(segundoAnexo!.UrlConteudo);
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await download.Content.ReadAsByteArrayAsync()).Should().StartWith(Encoding.ASCII.GetBytes("%PDF-"));

        var delete = await client.DeleteAsync(
            $"/api/v1/anexos/contas-pagar/{contaPagarId}/{segundoAnexo.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var anexosDepois = await client.GetFromJsonAsync<IReadOnlyCollection<AnexoResponse>>(
            $"/api/v1/anexos/contas-pagar/{contaPagarId}");
        anexosDepois.Should().ContainSingle();
    }

    [Fact]
    public async Task Upload_ComMimePdfEConteudoInvalido_DeveRetornarBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var contaPagarId = await CriarContaPagarAsync(client, fixture);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("não é pdf"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "arquivo", "falso.pdf");

        var response = await client.PostAsync($"/api/v1/anexos/contas-pagar/{contaPagarId}", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> CriarFaturaAsync(Guid cartaoId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var familiaId = await db.Cartoes.Where(x => x.Id == cartaoId).Select(x => x.FamiliaId).SingleAsync();
        var fatura = FaturaCartao.Criar(
            cartaoId,
            "2026-06",
            new DateOnly(2026, 6, 10),
            new DateOnly(2026, 6, 20),
            100m,
            null);
        fatura.AtribuirFamilia(familiaId);
        db.FaturasCartao.Add(fatura);
        await db.SaveChangesAsync();
        return fatura.Id;
    }

    private static async Task<HttpResponseMessage> UploadPdfAsync(HttpClient client, string tipo, Guid id, string nome)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.7\nconteudo de teste"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "arquivo", nome);
        return await client.PostAsync($"/api/v1/anexos/{tipo}/{id}", form);
    }

    private static async Task<Guid> CriarContaPagarAsync(HttpClient client, FinancialFixtureSeed.FixtureIds fixture)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-06-01",
            responsavelCompraId = fixture.ResponsavelId,
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-06-20",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 100m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta com anexo",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 100m } }
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private static async Task<Guid> CriarContaReceberAsync(HttpClient client, FinancialFixtureSeed.FixtureIds fixture)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-06-01",
            responsavelId = fixture.ResponsavelId,
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-06-20",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 200m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Receita com anexo",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 200m } }
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private static async Task<Guid> CriarCompraPlanejadaAsync(HttpClient client, FinancialFixtureSeed.FixtureIds fixture)
    {
        var response = await client.PostAsJsonAsync("/api/v1/compras-planejadas", new
        {
            titulo = "Notebook com orçamento",
            valorEstimado = 5000m,
            prioridade = "Alta",
            status = "Planejada",
            parcelavel = true,
            quantidadeParcelasDesejada = 10,
            contaGerencialId = fixture.ContaGerencialDespesaId,
            responsavelId = fixture.ResponsavelId
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private sealed record IdResponse(Guid Id);
}
