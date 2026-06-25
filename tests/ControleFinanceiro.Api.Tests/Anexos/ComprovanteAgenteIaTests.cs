using System.Text.Json;
using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.FinanceAI.Tools;
using ControleFinanceiro.Domain.Anexos;
using ControleFinanceiro.Domain.FinanceAI;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.Identidade;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Api.Tests.Anexos;

public sealed class ComprovanteAgenteIaTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task CriarLancamentoCartao_DeveGerarParcelasEVincularComprovantePendente()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var anexoService = scope.ServiceProvider.GetRequiredService<ControleFinanceiro.Application.Anexos.AnexoAppService>();
        var tool = new CriarLancamentoTool(db, anexoService);
        var familiaId = await db.Cartoes.Where(x => x.Id == fixture.CartaoId).Select(x => x.FamiliaId).SingleAsync();
        db.DefinirFamiliaCorrente(familiaId);

        var conversa = AiConversa.Criar(familiaId, Guid.NewGuid(), CanalAi.WhatsApp, "5511999999999");
        db.AiConversas.Add(conversa);
        var anexo = Anexo.Criar(
            "comprovante.jpg",
            "App_Data/anexos/teste/comprovante.jpg",
            "image/jpeg",
            128,
            new string('a', 64),
            OrigemAnexo.Whatsapp,
            conversa.Id,
            null);
        anexo.AtribuirFamilia(familiaId);
        db.Anexos.Add(anexo);
        await db.SaveChangesAsync();

        var resultJson = await tool.ExecuteAsync(
            JsonSerializer.Serialize(new
            {
                descricao = "Notebook",
                valor = 1200m,
                quantidadeParcelas = 3,
                contaGerencialId = fixture.ContaGerencialDespesaId,
                dataVencimento = "2026-06-22",
                responsavelId = fixture.ResponsavelId,
                recebedorId = fixture.RecebedorId,
                formaPagamentoId = fixture.FormaPagamentoCartaoId,
                cartaoId = fixture.CartaoId
            }),
            new ToolContext(familiaId, Guid.NewGuid(), PapelFamilia.Administrador, "Família teste", conversa.Id),
            CancellationToken.None);

        resultJson.Should().Contain("sucesso");
        var contas = await db.ContasPagar
            .Where(x => x.Descricao.StartsWith("Notebook"))
            .OrderBy(x => x.NumeroParcela)
            .ToListAsync();
        contas.Should().HaveCount(3);
        contas.Should().OnlyContain(x => x.StatusContaId == StatusConta.EmFaturaId);
        contas.Sum(x => x.ValorLiquido).Should().Be(1200m);

        var vinculos = await db.AnexoVinculos.Where(x => x.AnexoId == anexo.Id).ToListAsync();
        vinculos.Should().HaveCount(3);
        vinculos.Should().OnlyContain(x => x.TipoEntidade == TipoEntidadeAnexo.ContaPagar);
        vinculos.Select(x => x.EntidadeId).Should().BeEquivalentTo(contas.Select(x => x.Id));
    }
}
