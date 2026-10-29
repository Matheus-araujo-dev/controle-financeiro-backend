using System.Net.Http.Json;
using System.Text.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.FinanceAI;
using ControleFinanceiro.Application.Financeiro.Importacao;
using ControleFinanceiro.Contracts.Conciliacao;
using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class ConciliacaoFaturaIaTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Upload_AnalisaContasDoCiclo_SemAlterarFinancas(bool falhar)
    {
        var llm = new LlmFake(falhar);
        await using var factory = new CustomWebApplicationFactory(services => {
            services.RemoveAll<ILlmClient>(); services.AddSingleton<ILlmClient>(llm);
            services.RemoveAll<IPdfFaturaReader>(); services.AddSingleton<IPdfFaturaReader>(new ReaderFake());
        });
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        Guid invoiceId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var category = await db.ContasGerenciais.IgnoreQueryFilters().SingleAsync(x => x.Id == fixture.ContaGerencialDespesaId);
            db.DefinirFamiliaCorrente(category.FamiliaId);
            var invoice = FaturaCartao.Criar(fixture.CartaoId, "2026-09", new DateOnly(2026,9,10), new DateOnly(2026,9,20), 600m, null);
            db.FaturasCartao.Add(invoice);
            foreach (var month in new[] { 9, 10 })
            {
                var account = ContaPagar.Criar(null, new DateOnly(2026,7,7), null, fixture.RecebedorId,
                    new DateOnly(2026,month,15), fixture.FormaPagamentoCartaoId, fixture.CartaoId, null,
                    484.34m, 0, 0, 0, 3, 3, Guid.NewGuid(), null, month == 9 ? "Meias Pilates" : "OUTRO CICLO", null,
                    StatusConta.EmFaturaId, false, null, OrigemLancamento.Manual,
                    [RateioPlano.Create(fixture.ContaGerencialDespesaId, 484.34m)]);
                db.ContasPagar.Add(account); db.RateiosContaGerencial.AddRange(account.Rateios);
                if (month == 9) llm.ContaId = account.Id;
            }
            await db.SaveChangesAsync(); invoiceId = invoice.Id;
        }
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([1,2,3]), "arquivo", "teste.pdf");
        var response = await client.PostAsync($"/api/v1/faturas/{invoiceId}/conciliacoes", form);
        response.EnsureSuccessStatusCode();
        var review = (await response.Content.ReadFromJsonAsync<ConciliacaoFaturaResponse>())!;
        llm.Calls.Should().Be(1);
        llm.Prompt.Should().Contain("Meias Pilates").And.NotContain("OUTRO CICLO");
        review.ContasSistema.Should().ContainSingle().Which.Id.Should().Be(llm.ContaId);
        var item = review.Itens.Single();
        item.Status.Should().Be("Pendente"); item.ContaPagarVinculadaId.Should().BeNull();
        if (!falhar)
        {
            item.Candidatos.Should().ContainSingle().Which.Should().Match<CandidatoConciliacaoResponse>(x =>
                x.ContaId == llm.ContaId && !x.CorrespondenciaClara && x.Diferenca == -0.01m && x.Motivo.StartsWith("IA:"));
            var resumed = await client.GetFromJsonAsync<ConciliacaoFaturaResponse>($"/api/v1/faturas/{invoiceId}/conciliacoes/{review.Id}");
            resumed!.Itens.Single().Candidatos.Should().BeEquivalentTo(item.Candidatos);
            llm.Calls.Should().Be(1, "consultar a revisão não repete chamadas externas");
            using var sameFile = new MultipartFormDataContent();
            sameFile.Add(new ByteArrayContent([1,2,3]), "arquivo", "teste.pdf");
            var repeated = await client.PostAsync($"/api/v1/faturas/{invoiceId}/conciliacoes", sameFile);
            repeated.EnsureSuccessStatusCode();
            (await repeated.Content.ReadFromJsonAsync<ConciliacaoFaturaResponse>())!.Id.Should().Be(review.Id);
            llm.Calls.Should().Be(1, "reanexar uma revisão já analisada preserva o rascunho");
        }
        using var verify = factory.Services.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<IAppDbContext>();
        var accounts = await context.ContasPagar.IgnoreQueryFilters().ToListAsync();
        accounts.Should().HaveCount(2).And.OnlyContain(x => x.ValorLiquido == 484.34m && x.FaturaCartaoId == null);
        (await context.FaturasCartao.IgnoreQueryFilters().SingleAsync(x => x.Id == invoiceId)).ValorTotal.Should().Be(600m);
    }

    private sealed class ReaderFake : IPdfFaturaReader
    {
        public Task<CsvFaturaParser.ParseResult> ParseAsync(Stream stream, CancellationToken ct, DateOnly? vencimentoSelecionado = null, string? senha = null)
            => Task.FromResult(new CsvFaturaParser.ParseResult([
                new CsvFaturaItem(new DateOnly(2026,9,18), "Sb Fit", 484.33m, new DateOnly(2026,9,20), 3, 3)], null));
    }
    private sealed class LlmFake(bool falhar) : ILlmClient
    {
        public Guid ContaId { get; set; }
        public int Calls { get; private set; }
        public string Prompt { get; private set; } = "";
        public Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken ct)
        {
            Calls++; Prompt = request.SystemPrompt;
            if (falhar) throw new InvalidOperationException("LLM indisponível");
            var text = request.Messages.Single().Content;
            using var items = JsonDocument.Parse(text[text.IndexOf('[')..]);
            var itemId = items.RootElement[0].GetProperty("itemId").GetGuid();
            return Task.FromResult(new LlmCompletion($$"""{"sugestoes":[{"itemId":"{{itemId}}","contaPagarId":"{{ContaId}}","confianca":0.95,"justificativa":"Mesma parcela; diferença de um centavo"}]}""", [], new(1,1), "stop"));
        }
    }
}
