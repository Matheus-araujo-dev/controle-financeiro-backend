using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Importacoes;

public sealed class ImportacaoWhatsappTests
{
    [Fact]
    public void ProcessarResultado_DeveMarcarPendenteRevisaoQuandoHouverItensSugeridos()
    {
        var importacao = ImportacaoWhatsapp.CriarRecebida(
            TipoOrigemImportacaoWhatsapp.Texto,
            "5511999999999",
            "Pagar boleto de 120,50 amanha",
            null,
            null,
            null);

        importacao.MarcarEmProcessamento();
        importacao.RegistrarExtracaoComSucesso(0.91m);
        importacao.SubstituirItens(
        [
            ItemImportadoWhatsapp.Criar(
                importacao.Id,
                TipoSugestaoImportacaoWhatsapp.ContaPagar,
                """{"descricao":"Boleto","valor":120.50}""",
                "BOLETO")
        ]);

        importacao.Status.Should().Be(StatusImportacaoWhatsapp.PendenteRevisao);
        importacao.ConfiancaExtracao.Should().Be(0.91m);
        importacao.ProcessadoEmUtc.Should().NotBeNull();
        importacao.Itens.Should().ContainSingle();
    }

    [Fact]
    public void AtualizarStatusRevisao_DeveManterImportacaoPendenteAteAprovacaoExplicita()
    {
        var importacao = CriarImportacaoComItem();
        var item = importacao.Itens.Single();

        item.Confirmar("Confirmado pelo operador", "URBANUS BURGUER", null, null, null, true);
        importacao.AtualizarStatusRevisao();

        importacao.Status.Should().Be(StatusImportacaoWhatsapp.PendenteRevisao);
        importacao.ConfirmadoEmUtc.Should().BeNull();
        importacao.RejeitadoEmUtc.Should().BeNull();
        item.DescricaoAjustada.Should().Be("URBANUS BURGUER");
        item.MarcarComoRecorrente.Should().BeTrue();
    }

    [Fact]
    public void AprovarRevisao_DeveMarcarImportacaoComoConfirmadaQuandoNaoHouverPendencias()
    {
        var importacao = CriarImportacaoComItem();
        var item = importacao.Itens.Single();

        item.Confirmar("Confirmado pelo operador", null, null, null, null, false);
        importacao.AtualizarStatusRevisao();
        importacao.AprovarRevisao();

        importacao.Status.Should().Be(StatusImportacaoWhatsapp.Confirmado);
        importacao.ConfirmadoEmUtc.Should().NotBeNull();
        importacao.RejeitadoEmUtc.Should().BeNull();
    }

    [Fact]
    public void AtualizarConfirmacao_DevePermitirEditarItemJaConfirmado()
    {
        var importacao = CriarImportacaoComItem();
        var item = importacao.Itens.Single();

        item.Confirmar("Primeira revisao", null, null, null, null, false);
        item.AtualizarConfirmacao("Revisao ajustada", null, null, null, null, false);

        item.Observacao.Should().Be("Revisao ajustada");
        item.ConfirmadoEmUtc.Should().NotBeNull();
    }

    [Fact]
    public void ReabrirRevisao_DeveDestravarImportacaoEManterStatusEDadosDoItemConfirmado()
    {
        var importacao = CriarImportacaoComItem();
        var item = importacao.Itens.Single();

        item.Confirmar("Primeira revisao", "Nome amigavel", null, null, null, true);
        importacao.AtualizarStatusRevisao();
        importacao.AprovarRevisao();

        importacao.ReabrirRevisao();

        importacao.Status.Should().Be(StatusImportacaoWhatsapp.PendenteRevisao);
        importacao.ConfirmadoEmUtc.Should().BeNull();
        item.Status.Should().Be(StatusItemImportadoWhatsapp.Confirmado);
        item.DescricaoAjustada.Should().Be("Nome amigavel");
        item.MarcarComoRecorrente.Should().BeTrue();
    }

    private static ImportacaoWhatsapp CriarImportacaoComItem()
    {
        var importacao = ImportacaoWhatsapp.CriarRecebida(
            TipoOrigemImportacaoWhatsapp.Texto,
            "5511999999999",
            "Recebido pix de 80,00",
            null,
            null,
            null);

        importacao.MarcarEmProcessamento();
        importacao.RegistrarExtracaoComSucesso(0.84m);
        importacao.SubstituirItens(
        [
            ItemImportadoWhatsapp.Criar(
                importacao.Id,
                TipoSugestaoImportacaoWhatsapp.ContaReceber,
                """{"descricao":"Pix","valor":80.00}""",
                "PIX")
        ]);

        return importacao;
    }
}
