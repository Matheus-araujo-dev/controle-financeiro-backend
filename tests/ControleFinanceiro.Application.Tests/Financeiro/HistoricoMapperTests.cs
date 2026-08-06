using System.Globalization;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Financeiro.Common;
using FluentAssertions;

namespace ControleFinanceiro.Application.Tests.Financeiro;

public sealed class HistoricoMapperTests
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    private static AuditEntryDto MakeEntry(
        string action,
        string? beforeJson = null,
        string? afterJson = null,
        string executedBy = "usuario@teste.com") =>
        new(
            Guid.NewGuid(), "ContaPagar", Guid.NewGuid(),
            action, executedBy, new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc),
            beforeJson, afterJson);

    // --- Inferencia de acao ---

    [Fact]
    public void Mapear_QuandoCreated_DeveRetornarCriacao()
    {
        var entry = MakeEntry("Created", afterJson: """{"Descricao":"Compra"}""");
        var result = HistoricoMapper.Mapear(entry);
        result.Acao.Should().Be("Criação");
    }

    [Fact]
    public void Mapear_QuandoDeleted_DeveRetornarExclusao()
    {
        var entry = MakeEntry("Deleted", beforeJson: """{"Descricao":"Compra"}""");
        var result = HistoricoMapper.Mapear(entry);
        result.Acao.Should().Be("Exclusão");
    }

    [Fact]
    public void Mapear_QuandoUpdatedComDataLiquidacaoNullParaValor_DeveRetornarLiquidacao()
    {
        var before = """{"DataLiquidacao":null,"Descricao":"Compra"}""";
        var after = """{"DataLiquidacao":"2026-08-01","Descricao":"Compra"}""";
        var entry = MakeEntry("Updated", before, after);

        var result = HistoricoMapper.Mapear(entry);
        result.Acao.Should().Be("Liquidação");
    }

    [Fact]
    public void Mapear_QuandoUpdatedComDataLiquidacaoValorParaNull_DeveRetornarEstorno()
    {
        var before = """{"DataLiquidacao":"2026-08-01","Descricao":"Compra"}""";
        var after = """{"DataLiquidacao":null,"Descricao":"Compra"}""";
        var entry = MakeEntry("Updated", before, after);

        var result = HistoricoMapper.Mapear(entry);
        result.Acao.Should().Be("Estorno");
    }

    [Fact]
    public void Mapear_QuandoUpdatedSemAlteracaoLiquidacao_DeveRetornarEdicao()
    {
        var before = """{"DataLiquidacao":null,"Descricao":"Antigo"}""";
        var after = """{"DataLiquidacao":null,"Descricao":"Novo"}""";
        var entry = MakeEntry("Updated", before, after);

        var result = HistoricoMapper.Mapear(entry);
        result.Acao.Should().Be("Edição");
    }

    // --- Campos de alteracao ---

    [Fact]
    public void Mapear_QuandoCreated_DeveExibirCamposIniciaisPreenchidos()
    {
        var entry = MakeEntry("Created", afterJson: """{"Descricao":"Compra","ValorOriginal":100}""");
        var result = HistoricoMapper.Mapear(entry);
        result.Alteracoes.Should().HaveCount(2);
        result.Alteracoes.Should().AllSatisfy(a => a.Antes.Should().BeNull());
        result.Alteracoes.Single(a => a.Campo == "Descrição").Depois.Should().Be("Compra");
    }

    [Fact]
    public void Mapear_QuandoCreated_NaoDeveIncluirCamposNulos()
    {
        var entry = MakeEntry("Created", afterJson: """{"Descricao":"Compra","DataLiquidacao":null}""");
        var result = HistoricoMapper.Mapear(entry);
        result.Alteracoes.Should().ContainSingle(a => a.Campo == "Descrição");
    }

    [Fact]
    public void Mapear_QuandoUpdated_DeveListarCamposAlterados()
    {
        var before = """{"Descricao":"Antigo","ValorOriginal":50.00}""";
        var after = """{"Descricao":"Novo","ValorOriginal":100.00}""";
        var entry = MakeEntry("Updated", before, after);

        var result = HistoricoMapper.Mapear(entry);

        result.Alteracoes.Should().HaveCount(2);

        var descricao = result.Alteracoes.Single(a => a.Campo == "Descrição");
        descricao.Antes.Should().Be("Antigo");
        descricao.Depois.Should().Be("Novo");

        // Usa a mesma cultura para gerar o valor esperado e evitar divergencia de espaço
        var valorEsperadoAntes = 50m.ToString("C2", PtBr);
        var valorEsperadoDepois = 100m.ToString("C2", PtBr);
        var valor = result.Alteracoes.Single(a => a.Campo == "Valor Original");
        valor.Antes.Should().Be(valorEsperadoAntes);
        valor.Depois.Should().Be(valorEsperadoDepois);
    }

    [Fact]
    public void Mapear_QuandoCampoNaoAlterado_NaoDeveIncluirNaLista()
    {
        var before = """{"Descricao":"Mesmo","ValorOriginal":100.00}""";
        var after = """{"Descricao":"Mesmo","ValorOriginal":200.00}""";
        var entry = MakeEntry("Updated", before, after);

        var result = HistoricoMapper.Mapear(entry);

        result.Alteracoes.Should().HaveCount(1);
        result.Alteracoes.Single().Campo.Should().Be("Valor Original");
    }

    [Fact]
    public void Mapear_QuandoCampoIgnorado_NaoDeveIncluirNaLista()
    {
        var before = """{"Id":"a0000000-0000-0000-0000-000000000001","FamiliaId":"b0000000-0000-0000-0000-000000000001"}""";
        var after = """{"Id":"a0000000-0000-0000-0000-000000000001","FamiliaId":"c0000000-0000-0000-0000-000000000001"}""";
        var entry = MakeEntry("Updated", before, after);

        var result = HistoricoMapper.Mapear(entry);

        result.Alteracoes.Should().BeEmpty();
    }

    [Fact]
    public void Mapear_QuandoDataAlterada_DeveFormatarNoPadraoBr()
    {
        var before = """{"DataVencimento":"2026-08-01"}""";
        var after = """{"DataVencimento":"2026-09-01"}""";
        var entry = MakeEntry("Updated", before, after);

        var result = HistoricoMapper.Mapear(entry);

        var campo = result.Alteracoes.Single(a => a.Campo == "Vencimento");
        campo.Antes.Should().Be("01/08/2026");
        campo.Depois.Should().Be("01/09/2026");
    }

    [Fact]
    public void Mapear_QuandoBooleanAlterado_DeveFormatarSimNao()
    {
        var before = """{"EhRecorrente":false}""";
        var after = """{"EhRecorrente":true}""";
        var entry = MakeEntry("Updated", before, after);

        var result = HistoricoMapper.Mapear(entry);

        var campo = result.Alteracoes.Single(a => a.Campo == "Recorrente");
        campo.Antes.Should().Be("Não");
        campo.Depois.Should().Be("Sim");
    }

    // --- Metadados ---

    [Fact]
    public void Mapear_DevePreencherMetadados()
    {
        var id = Guid.NewGuid();
        var entry = new AuditEntryDto(
            id, "ContaPagar", Guid.NewGuid(), "Created",
            "matheus@empresa.com", new DateTime(2026, 7, 10, 15, 30, 0, DateTimeKind.Utc),
            null, """{"Descricao":"X"}""");

        var result = HistoricoMapper.Mapear(entry);

        result.Id.Should().Be(id);
        result.RealizadoPor.Should().Be("matheus@empresa.com");
        result.OcorreuEmUtc.Should().Be(new DateTime(2026, 7, 10, 15, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Mapear_QuandoExecutedBySystemSemRecorrencia_DeveExibirSistema()
    {
        var entry = MakeEntry("Created", afterJson: """{"Descricao":"X"}""", executedBy: "system");
        var result = HistoricoMapper.Mapear(entry);
        result.RealizadoPor.Should().Be("Sistema");
    }

    [Fact]
    public void Mapear_QuandoExecutedBySystemComRecorrencia_DeveExibirRecorrenciaAutomatica()
    {
        var recorrenciaId = Guid.NewGuid();
        var afterJson = $$"""{"Descricao":"X","RegraRecorrenciaId":"{{recorrenciaId}}"}""";
        var entry = MakeEntry("Created", afterJson: afterJson, executedBy: "system");
        var result = HistoricoMapper.Mapear(entry);
        result.RealizadoPor.Should().Be("Recorrência automática");
        result.RegraRecorrenciaId.Should().Be(recorrenciaId);
    }

    [Fact]
    public void Mapear_QuandoExecutedByEmail_DeveExibirEmail()
    {
        var entry = MakeEntry("Created", afterJson: """{"Descricao":"X"}""", executedBy: "user@empresa.com");
        var result = HistoricoMapper.Mapear(entry);
        result.RealizadoPor.Should().Be("user@empresa.com");
    }

    [Fact]
    public void Mapear_ListaVazia_DeveRetornarVazio()
    {
        var result = HistoricoMapper.Mapear([]);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Mapear_Lista_DeveOrdenarPorOcorrencia()
    {
        var e1 = new AuditEntryDto(Guid.NewGuid(), "ContaPagar", Guid.NewGuid(), "Created",
            "u@u.com", new DateTime(2026, 1, 1), null, """{"Descricao":"A"}""");
        var e2 = new AuditEntryDto(Guid.NewGuid(), "ContaPagar", Guid.NewGuid(), "Updated",
            "u@u.com", new DateTime(2026, 1, 2),
            """{"Descricao":"A"}""", """{"Descricao":"B"}""");

        var result = HistoricoMapper.Mapear([e1, e2]);

        result.Should().HaveCount(2);
        result[0].OcorreuEmUtc.Should().BeBefore(result[1].OcorreuEmUtc);
    }
}
