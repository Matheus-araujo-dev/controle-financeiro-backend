using ControleFinanceiro.Domain.Identidade;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using ControleFinanceiro.Infrastructure.Migrations;
using ControleFinanceiro.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ControleFinanceiro.Infrastructure.Tests.Persistence;

public sealed class LegacyImportedWorkspaceMigrationTests
{
    [Fact]
    public async Task Migracao_DeveRecuperarSomenteProprietarioComprovadoSemAlterarDadosJaAtribuidos()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var familia = Familia.Criar("Proprietario comprovado");
        db.Familias.Add(familia);
        var importacao = ImportacaoWhatsapp.CriarRecebida(TipoOrigemImportacaoWhatsapp.Pdf, "5511999999999", null, "fatura.pdf", null, "application/pdf");
        importacao.AtribuirFamilia(familia.Id);
        var recuperavel = ItemImportadoWhatsapp.Criar(importacao.Id, TipoSugestaoImportacaoWhatsapp.CompraCartao, "{}", null);
        var existente = ItemImportadoWhatsapp.Criar(importacao.Id, TipoSugestaoImportacaoWhatsapp.CompraCartao, "{}", null);
        var familiaExistente = Guid.NewGuid();
        existente.AtribuirFamilia(familiaExistente);
        var semProprietario = ImportacaoWhatsapp.CriarRecebida(TipoOrigemImportacaoWhatsapp.Pdf, "5511888888888", null, "fatura.pdf", null, "application/pdf");
        var desconhecido = ItemImportadoWhatsapp.Criar(semProprietario.Id, TipoSugestaoImportacaoWhatsapp.CompraCartao, "{}", null);
        db.ImportacoesWhatsapp.AddRange(importacao, semProprietario);
        db.ItensImportadosWhatsapp.AddRange(recuperavel, existente, desconhecido);
        await db.SaveChangesAsync();
        var migration = new RepairLegacyImportedItemWorkspace();
        foreach (var sql in migration.UpOperations.OfType<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(sql.Sql);
        db.ChangeTracker.Clear();
        var ids = await db.ItensImportadosWhatsapp.IgnoreQueryFilters().ToDictionaryAsync(i => i.Id, i => i.FamiliaId);
        ids[recuperavel.Id].Should().Be(familia.Id);
        ids[existente.Id].Should().Be(familiaExistente);
        ids[desconhecido.Id].Should().Be(Guid.Empty);
        foreach (var sql in migration.UpOperations.OfType<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(sql.Sql);
        (await db.ItensImportadosWhatsapp.IgnoreQueryFilters().ToDictionaryAsync(i => i.Id, i => i.FamiliaId)).Should().BeEquivalentTo(ids);
    }
}
