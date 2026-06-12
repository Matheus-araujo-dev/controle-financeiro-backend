using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using FluentAssertions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class RecorrenciasControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Get_DeveRetornarDescricaoValorPessoaEResponsavelDaContaOrigem()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var criarContaPagarResponse = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = "2026-04-04",
            responsavelCompraId = fixture.ResponsavelId,
            recebedorId = fixture.RecebedorId,
            dataVencimento = "2026-04-08",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 450m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Aluguel da sede",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 450m }
            },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                tipoDia = "DiaFixo",
                diaOrdemMensal = 8,
                dataInicio = (string?)null,
                dataFim = "2026-08-01",
                permiteEdicaoOcorrenciaIndividual = true,
                observacao = "Recorrência de aluguel"
            }
        });

        var contaPagarCriada = await criarContaPagarResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        var criarContaReceberResponse = await client.PostAsJsonAsync("/api/v1/contas-receber", new
        {
            dataEmissao = "2026-04-05",
            responsavelId = fixture.ResponsavelId,
            pagadorId = fixture.PagadorId,
            dataVencimento = "2026-04-15",
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 1200m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Mensalidade de consultoria",
            rateios = new[]
            {
                new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 1200m }
            },
            recorrencia = new
            {
                tipoPeriodicidade = "Mensal",
                tipoDia = "DiaFixo",
                diaOrdemMensal = 15,
                dataInicio = (string?)null,
                dataFim = "2026-09-01",
                permiteEdicaoOcorrenciaIndividual = false,
                observacao = "Recorrência de consultoria"
            }
        });

        var contaReceberCriada = await criarContaReceberResponse.Content.ReadFromJsonAsync<ContaDetalheResponse>();

        var recorrencias = await client.GetFromJsonAsync<RecorrenciaListResponse>("/api/v1/recorrencias");

        criarContaPagarResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        criarContaReceberResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        recorrencias.Should().NotBeNull();
        recorrencias!.Items.Should().HaveCount(2);
        recorrencias.Summary.TotalRegistros.Should().Be(2);
        recorrencias.Summary.ValorTotal.Should().Be(1650m);

        recorrencias.Items.Should().Contain(item =>
            item.Id == contaPagarCriada!.Recorrencia!.Id &&
            item.ContaOrigemTipo == "ContaPagar" &&
            item.Descricao == "Aluguel da sede" &&
            item.ValorLiquido == 450m &&
            item.PessoaNome == "Fornecedor Fase 3" &&
            item.ResponsavelNome == "Responsavel Fase 3");

        recorrencias.Items.Should().Contain(item =>
            item.Id == contaReceberCriada!.Recorrencia!.Id &&
            item.ContaOrigemTipo == "ContaReceber" &&
            item.Descricao == "Mensalidade de consultoria" &&
            item.ValorLiquido == 1200m &&
            item.PessoaNome == "Cliente Fase 3" &&
            item.ResponsavelNome == "Responsavel Fase 3");
    }

    private sealed record ContaDetalheResponse(Guid Id, RecorrenciaResponse? Recorrencia);

    private sealed record RecorrenciaResponse(Guid Id);

    private sealed record RecorrenciaListItemResponse(
        Guid Id,
        string TipoPeriodicidade,
        string TipoDia,
        int DiaOrdemMensal,
        DateOnly DataInicio,
        DateOnly? DataFim,
        bool Ativa,
        bool PermiteEdicaoOcorrenciaIndividual,
        string? Observacao,
        string ContaOrigemTipo,
        Guid ContaOrigemId,
        string Descricao,
        decimal ValorLiquido,
        string PessoaNome,
        string? ResponsavelNome);

    private sealed record RecorrenciaListSummaryResponse(int TotalRegistros, decimal ValorTotal);

    private sealed record RecorrenciaListResponse(
        IReadOnlyCollection<RecorrenciaListItemResponse> Items,
        RecorrenciaListSummaryResponse Summary);
}
