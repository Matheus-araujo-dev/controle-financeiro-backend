using System.Net;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Financeiro.Importacao;
using ControleFinanceiro.Contracts.Conciliacao;
using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public class ConciliacaoFaturaUploadTests
{
    [Fact]
    public async Task UploadMaiorQueSeisMb_UsaFaturaSelecionadaERetomaSemDuplicar()
    {
        var reader = new Reader();
        await using var factory = new CustomWebApplicationFactory(services => {
            services.RemoveAll<IPdfFaturaReader>(); services.AddSingleton<IPdfFaturaReader>(reader);
        });
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        Guid invoiceId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var category = await db.ContasGerenciais.IgnoreQueryFilters().SingleAsync(x => x.Id == fixture.ContaGerencialDespesaId);
            db.DefinirFamiliaCorrente(category.FamiliaId);
            var invoice = FaturaCartao.Criar(fixture.CartaoId, "2026-10", new DateOnly(2026,10,10), new DateOnly(2026,10,20), 100m, null);
            db.FaturasCartao.Add(invoice); await db.SaveChangesAsync(); invoiceId = invoice.Id;
        }
        async Task<ConciliacaoFaturaResponse> Upload()
        {
            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(new byte[7 * 1024 * 1024]), "arquivo", "extrato.pdf");
            var response = await client.PostAsync($"/api/v1/faturas/{invoiceId}/conciliacoes", content);
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<ConciliacaoFaturaResponse>())!;
        }
        var first = await Upload();
        var second = await Upload();
        first.Id.Should().Be(second.Id);
        reader.Calls.Should().Be(1);
        reader.Length.Should().Be(7 * 1024 * 1024);
        reader.Due.Should().Be(new DateOnly(2026,10,20));
        using var verification = factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<IAppDbContext>();
        (await context.ContasPagar.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    private sealed class Reader : IPdfFaturaReader
    {
        public int Calls { get; private set; }
        public long Length { get; private set; }
        public DateOnly? Due { get; private set; }
        public Task<CsvFaturaParser.ParseResult> ParseAsync(Stream stream, CancellationToken ct, DateOnly? vencimentoSelecionado = null)
        {
            Calls++; Length = stream.Length; Due = vencimentoSelecionado;
            stream.Position.Should().Be(0);
            return Task.FromResult(new CsvFaturaParser.ParseResult([new(new DateOnly(2026,9,22), "LOJA", 100, Due)], null));
        }
    }
}
