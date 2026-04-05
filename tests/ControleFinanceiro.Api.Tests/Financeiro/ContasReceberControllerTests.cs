using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class ContasReceberControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

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
            contaBancariaId = fixture.ContaBancariaId
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
                diaGeracaoMensal = 25,
                dataInicio = "2026-04-25",
                dataFim = (string?)null,
                permiteEdicaoOcorrenciaIndividual = true
            }
        });

        var created = await createResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        var gerarResponse = await client.PostAsJsonAsync($"/api/v1/contas-receber/{created!.Id}/gerar-ocorrencias", new
        {
            ateData = "2026-06-30"
        });

        var listResponse = await client.GetFromJsonAsync<PagedResponse<ContaResumoResponse>>("/api/v1/contas-receber?search=Mensalidade");

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        gerarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        listResponse.Should().NotBeNull();
        listResponse!.Items.Should().HaveCount(3);
        listResponse.Items.Should().OnlyContain(item => item.EhRecorrente);
    }

    private sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

    private sealed record ContaDetalheResponse(Guid Id, string Descricao, decimal ValorLiquido, int QuantidadeParcelas, int NumeroParcela);

    private sealed record ContaResumoResponse(Guid Id, string Descricao, decimal ValorLiquido, bool EhRecorrente);

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
