using System.Net;
using ControleFinanceiro.Contracts.Conciliacao;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Conciliacao;
using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Api.Tests.Financeiro;

public sealed class ConciliacaoFaturaTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task PreviaReembolso_DistribuiCentavosSemCriarContas()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        Guid invoiceId, sessionId, itemId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var category = await db.ContasGerenciais.IgnoreQueryFilters().SingleAsync(x => x.Id == fixture.ContaGerencialDespesaId);
            db.DefinirFamiliaCorrente(category.FamiliaId);
            var invoice = FaturaCartao.Criar(fixture.CartaoId, "2026-09", new DateOnly(2026,9,10), new DateOnly(2026,9,20), 100m, null);
            db.FaturasCartao.Add(invoice);
            var item = ItemConciliacao.CriarFatura(new DateOnly(2026,9,1), "COMPRA", 100m, "preview", 1, 1);
            var session = Domain.Conciliacao.Conciliacao.CriarFatura("teste.pdf", invoice.Id, "previewhash", [item]);
            db.Conciliacoes.Add(session);
            await db.SaveChangesAsync();
            invoiceId = invoice.Id; sessionId = session.Id; itemId = item.Id;
        }
        var url = $"/api/v1/faturas/{invoiceId}/conciliacoes/{sessionId}/itens/{itemId}/previa-reembolso";
        var response = await client.PostAsJsonAsync(url, new {
            contaPagarId = (Guid?)null, parcelarIgual = false, valorTotal = 100m,
            pagadoresIds = new[] { fixture.PagadorId, fixture.ResponsavelId, fixture.RecebedorId },
            dataVencimento = "2026-10-05"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var rows = (await response.Content.ReadFromJsonAsync<PreviaTeste[]>())!;
        rows.Select(x => x.Valor).Should().Equal(33.33m, 33.33m, 33.34m);
        rows.Should().OnlyContain(x => x.DataVencimento == new DateOnly(2026,10,5));
        using var verification = factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<IAppDbContext>();
        (await context.ContasReceber.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await context.ContasPagar.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        var invalid = await client.PostAsJsonAsync(url, new {
            contaPagarId = Guid.NewGuid(), parcelarIgual = true, valorTotal = 100m,
            pagadoresIds = new[] { fixture.PagadorId }, dataVencimento = "2026-10-05"
        });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record PreviaTeste(Guid PagadorId, int NumeroParcela, int QuantidadeParcelas, decimal Valor, DateOnly DataVencimento);

    [Fact]
    public async Task Vincular_AjustaContaExistenteSemDuplicar_ERepeticaoPreservaAuditoria()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        Guid invoiceId, sessionId, itemId, accountId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var category = await db.ContasGerenciais.IgnoreQueryFilters().SingleAsync(x => x.Id == fixture.ContaGerencialDespesaId);
            db.DefinirFamiliaCorrente(category.FamiliaId);
            var invoice = FaturaCartao.Criar(fixture.CartaoId, "2026-09", new DateOnly(2026,9,10), new DateOnly(2026,9,20), 33.34m, null);
            db.FaturasCartao.Add(invoice);
            var account = ContaPagar.Criar(null, new DateOnly(2026,9,1), null, fixture.RecebedorId,
                invoice.DataVencimento, fixture.FormaPagamentoCartaoId, fixture.CartaoId, null,
                33.34m, 0, 0, 0, 3, 2, Guid.NewGuid(), null, "Compra 2/3", null,
                StatusConta.EmFaturaId, false, null, OrigemLancamento.Manual,
                [RateioPlano.Create(fixture.ContaGerencialDespesaId, 33.34m)]);
            account.VincularFaturaCartao(invoice.Id);
            db.ContasPagar.Add(account);
            db.RateiosContaGerencial.AddRange(account.Rateios);
            var item = ItemConciliacao.CriarFatura(account.DataEmissao, "COMPRA", 33.33m, "key", 2, 3);
            var session = Domain.Conciliacao.Conciliacao.CriarFatura("teste.pdf", invoice.Id, "hash", [item]);
            db.Conciliacoes.Add(session);
            await db.SaveChangesAsync();
            invoiceId = invoice.Id; sessionId = session.Id; itemId = item.Id; accountId = account.Id;
        }
        var url = $"/api/v1/faturas/{invoiceId}/conciliacoes/{sessionId}/itens/{itemId}/vincular";
        var request = new { contaPagarId = accountId, valorEsperadoSistema = 33.34m, usarValorFatura = true };
        var semAceite = await client.PostAsJsonAsync(url, new { contaPagarId = accountId, valorEsperadoSistema = 33.34m, usarValorFatura = false });
        semAceite.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var desatualizado = await client.PostAsJsonAsync(url, new { contaPagarId = accountId, valorEsperadoSistema = 33.35m, usarValorFatura = true });
        desatualizado.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await client.PostAsJsonAsync(url, request);
        result.StatusCode.Should().Be(HttpStatusCode.NoContent, await result.Content.ReadAsStringAsync());
        (await client.PostAsJsonAsync(url, request)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var verification = factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<IAppDbContext>();
        var accounts = await context.ContasPagar.IgnoreQueryFilters().Where(x => x.CartaoId == fixture.CartaoId).ToListAsync();
        accounts.Should().ContainSingle();
        accounts[0].Id.Should().Be(accountId);
        accounts[0].ValorLiquido.Should().Be(33.33m);
        (await context.RateiosContaGerencial.IgnoreQueryFilters().Where(x => x.ContaPagarId == accountId).ToListAsync()).Sum(x => x.Valor).Should().Be(33.33m);
        accounts[0].NumeroParcela.Should().Be(2);
        var saved = await context.ItensConciliacao.IgnoreQueryFilters().SingleAsync(x => x.Id == itemId);
        saved.ValorAnteriorSistema.Should().Be(33.34m);
    }

    [Fact]
    public async Task Pdf_ReabreMesmaSessao_ESeparaComprasIdenticas()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        Guid invoiceId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var category = await db.ContasGerenciais.IgnoreQueryFilters().SingleAsync(x => x.Id == fixture.ContaGerencialDespesaId);
            db.DefinirFamiliaCorrente(category.FamiliaId);
            var invoice = FaturaCartao.Criar(fixture.CartaoId, "2026-04", new DateOnly(2026,4,3), new DateOnly(2026,4,13), 300m, null);
            db.FaturasCartao.Add(invoice);
            await db.SaveChangesAsync();
            invoiceId = invoice.Id;
        }
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        string[] lines = ["Aplicativo Bradesco Cartoes", "Data: 03/04/2026 - 07:54", "Vencimento", "13/04/2026",
            "CLIENTE EXEMPLO - VISA", "XXXX.XXXX.XXXX.1111", "05/03", "MERCADO", "150,00", "05/03", "MERCADO", "150,00"];
        for (var n = 0; n < lines.Length; n++) page.AddText(lines[n], 10, new PdfPoint(40, 800 - n * 15), font);
        var bytes = builder.Build();
        async Task<ConciliacaoFaturaResponse> Upload()
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(bytes), "arquivo", "bradesco.pdf");
            var response = await client.PostAsync($"/api/v1/faturas/{invoiceId}/conciliacoes", form);
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<ConciliacaoFaturaResponse>())!;
        }
        var first = await Upload();
        var again = await Upload();
        first.Id.Should().Be(again.Id);
        first.Itens.Should().HaveCount(2);
        first.Itens.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        first.ContasSistema.Should().BeEmpty();
        var reopened = await client.GetFromJsonAsync<ConciliacaoFaturaResponse>($"/api/v1/faturas/{invoiceId}/conciliacoes/{first.Id}");
        reopened!.Itens.Select(x => x.Id).Should().BeEquivalentTo(first.Itens.Select(x => x.Id));
        using var scope2 = factory.Services.CreateScope();
        var context = scope2.ServiceProvider.GetRequiredService<IAppDbContext>();
        (await context.ContasPagar.IgnoreQueryFilters().CountAsync(x => x.CartaoId == fixture.CartaoId)).Should().Be(0);
    }

    [Fact]
    public async Task CriarItem_ComReembolso_EAtomicoEIdempotente()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        Guid invoiceId, sessionId, itemId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var category = await db.ContasGerenciais.IgnoreQueryFilters().SingleAsync(x => x.Id == fixture.ContaGerencialDespesaId);
            db.DefinirFamiliaCorrente(category.FamiliaId);
            var invoice = FaturaCartao.Criar(fixture.CartaoId, "2026-09", new DateOnly(2026,9,10), new DateOnly(2026,9,20), 33.33m, null);
            var antiga = FaturaCartao.Criar(fixture.CartaoId, "2026-08", new DateOnly(2026,8,3), new DateOnly(2026,8,13), 33.33m, null);
            antiga.Fechar(); db.FaturasCartao.Add(antiga);
            var item = ItemConciliacao.CriarFatura(new DateOnly(2026,8,1), "LOJA", 33.33m, "nova", 2, 3);
            var session = Domain.Conciliacao.Conciliacao.CriarFatura("nova.pdf", invoice.Id, "nova", [item]);
            db.FaturasCartao.Add(invoice); db.Conciliacoes.Add(session);
            await db.SaveChangesAsync();
            invoiceId = invoice.Id; sessionId = session.Id; itemId = item.Id;
        }
        object Request(Guid pagadorId) => new {
            descricao = "Tênis de presente", recebedorId = fixture.RecebedorId, responsavelCompraId = fixture.ResponsavelId,
            formaPagamentoId = fixture.FormaPagamentoCartaoId,
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 33.33m } },
            reembolso = new { parcelarIgual = false, valorTotal = 33.33m, pagadoresIds = new[] { pagadorId },
                formaPagamentoId = fixture.FormaPagamentoManualId, dataVencimento = "2026-09-20", descricao = "Reembolso tênis",
                rateios = new[] { new { contaGerencialId = fixture.ContaGerencialReceitaId, valor = 33.33m } } }
        };
        var url = $"/api/v1/faturas/{invoiceId}/conciliacoes/{sessionId}/itens/{itemId}/criar";
        var invalid = await client.PostAsJsonAsync(url, Request(Guid.NewGuid()));
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest, await invalid.Content.ReadAsStringAsync());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            (await db.ContasPagar.IgnoreQueryFilters().CountAsync(x => x.CartaoId == fixture.CartaoId)).Should().Be(0);
            (await db.ItensConciliacao.IgnoreQueryFilters().SingleAsync(x => x.Id == itemId)).StatusItem.Should().Be(StatusItemConciliacao.Pendente);
            (await db.MemoriasEstabelecimento.IgnoreQueryFilters().CountAsync(x => x.CartaoId == fixture.CartaoId)).Should().Be(0);
        }
        var result = await client.PostAsJsonAsync(url, Request(fixture.PagadorId));
        result.StatusCode.Should().Be(HttpStatusCode.NoContent, await result.Content.ReadAsStringAsync());
        (await client.PostAsJsonAsync(url, Request(fixture.PagadorId))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var verification = factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<IAppDbContext>();
        var conta = await context.ContasPagar.IgnoreQueryFilters().SingleAsync(x => x.CartaoId == fixture.CartaoId);
        conta.NumeroParcela.Should().Be(2); conta.QuantidadeParcelas.Should().Be(3);
        conta.ValorLiquido.Should().Be(33.33m); conta.Descricao.Should().Be("Tênis de presente");
        conta.ResponsavelCompraId.Should().Be(fixture.ResponsavelId);
        conta.GrupoReembolsoId.Should().NotBeNull();
        (await context.ContasReceber.IgnoreQueryFilters().CountAsync(x => x.GrupoReembolsoId == conta.GrupoReembolsoId)).Should().Be(1);
        var memoria = await context.MemoriasEstabelecimento.IgnoreQueryFilters().SingleAsync(x => x.CartaoId == fixture.CartaoId);
        memoria.Confirmacoes.Should().Be(1);
        var review = await client.GetFromJsonAsync<ConciliacaoFaturaResponse>($"/api/v1/faturas/{invoiceId}/conciliacoes/{sessionId}");
        review!.Itens.Single().DescricaoOriginal.Should().Be("LOJA");
        review.Itens.Single().Preferencias!.Descricao.Should().Be("Tênis de presente");
        review.Itens.Single().Preferencias!.GerarReembolso.Should().BeTrue();
        review.Itens.Single().Preferencias!.ReembolsoPagadoresIds.Should().Contain(fixture.PagadorId);
    }

    [Fact]
    public async Task Rascunho_NaoCriaContaERejeitaEdicaoDesatualizada()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        Guid invoiceId, sessionId, itemId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var category = await db.ContasGerenciais.IgnoreQueryFilters().SingleAsync(x => x.Id == fixture.ContaGerencialDespesaId);
            db.DefinirFamiliaCorrente(category.FamiliaId);
            var invoice = FaturaCartao.Criar(fixture.CartaoId, "2026-09", new DateOnly(2026,9,10), new DateOnly(2026,9,20), 100m, null);
            var item = ItemConciliacao.CriarFatura(new DateOnly(2026,9,1), "ASAAS*NEXTFITago 26", 100m, "rascunho", 1, 1);
            var session = Domain.Conciliacao.Conciliacao.CriarFatura("rascunho.pdf", invoice.Id, "rascunho", [item]);
            db.FaturasCartao.Add(invoice); db.Conciliacoes.Add(session);
            await db.SaveChangesAsync();
            invoiceId = invoice.Id; sessionId = session.Id; itemId = item.Id;
        }
        var reviewUrl = $"/api/v1/faturas/{invoiceId}/conciliacoes/{sessionId}";
        var review = (await client.GetFromJsonAsync<ConciliacaoFaturaResponse>(reviewUrl))!;
        var request = new { dados = new { descricao = "Sistema do pilates", recebedorId = "" }, atualizadoEmUtc = review.Itens.Single().AtualizadoEmUtc };
        var saved = await client.PutAsJsonAsync($"{reviewUrl}/itens/{itemId}/rascunho", request);
        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        (await client.PutAsJsonAsync($"{reviewUrl}/itens/{itemId}/rascunho", request)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var reopened = (await client.GetFromJsonAsync<ConciliacaoFaturaResponse>(reviewUrl))!;
        reopened.Itens.Single().Rascunho!.Value.GetProperty("descricao").GetString().Should().Be("Sistema do pilates");
        using var verification = factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<IAppDbContext>();
        (await context.ContasPagar.IgnoreQueryFilters().CountAsync(x => x.CartaoId == fixture.CartaoId)).Should().Be(0);
        (await context.MemoriasEstabelecimento.IgnoreQueryFilters().CountAsync(x => x.CartaoId == fixture.CartaoId)).Should().Be(0);
    }

    [Fact]
    public async Task CriarRecorrente_RespeitaDataFimESemDuplicarMesAtual()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var inicio = new DateOnly(hoje.Year, hoje.Month, 1);
        var fim = inicio.AddMonths(3).AddDays(-1);
        Guid invoiceId, sessionId, itemId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var category = await db.ContasGerenciais.IgnoreQueryFilters().SingleAsync(x => x.Id == fixture.ContaGerencialDespesaId);
            db.DefinirFamiliaCorrente(category.FamiliaId);
            var invoice = FaturaCartao.Criar(fixture.CartaoId, inicio.ToString("yyyy-MM"), inicio.AddDays(2), inicio.AddDays(12), 100m, null);
            var item = ItemConciliacao.CriarFatura(inicio, "ASSINATURA", 100m, "recorrente", 1, 1);
            var session = Domain.Conciliacao.Conciliacao.CriarFatura("recorrente.pdf", invoice.Id, "recorrente", [item]);
            db.FaturasCartao.Add(invoice); db.Conciliacoes.Add(session);
            await db.SaveChangesAsync();
            invoiceId = invoice.Id; sessionId = session.Id; itemId = item.Id;
        }
        var request = new { descricao = "Assinatura", recebedorId = fixture.RecebedorId, responsavelCompraId = fixture.ResponsavelId,
            formaPagamentoId = fixture.FormaPagamentoCartaoId, rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 100m } },
            recorrencia = new { tipoPeriodicidade = "Mensal", tipoDia = "DiaFixo", diaOrdemMensal = 1, dataInicio = inicio, dataFim = fim, permiteEdicaoOcorrenciaIndividual = true } };
        var url = $"/api/v1/faturas/{invoiceId}/conciliacoes/{sessionId}/itens/{itemId}/criar";
        var result = await client.PostAsJsonAsync(url, request);
        result.StatusCode.Should().Be(HttpStatusCode.NoContent, await result.Content.ReadAsStringAsync());
        (await client.PostAsJsonAsync(url, request)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var verification = factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<IAppDbContext>();
        var accounts = await context.ContasPagar.IgnoreQueryFilters().Where(x => x.CartaoId == fixture.CartaoId).ToListAsync();
        accounts.Should().HaveCount(3);
        accounts.Select(x => x.DataEmissao).Should().OnlyHaveUniqueItems();
        accounts.Should().OnlyContain(x => x.EhRecorrente && x.StatusContaId == StatusConta.EmFaturaId);
    }
}

