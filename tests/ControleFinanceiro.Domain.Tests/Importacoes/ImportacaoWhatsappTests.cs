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
                """{"descricao":"Boleto","valor":120.50}""")
        ]);

        importacao.Status.Should().Be(StatusImportacaoWhatsapp.PendenteRevisao);
        importacao.ConfiancaExtracao.Should().Be(0.91m);
        importacao.ProcessadoEmUtc.Should().NotBeNull();
        importacao.Itens.Should().ContainSingle();
    }

    [Fact]
    public void AtualizarStatusRevisao_DeveMarcarImportacaoComoConfirmadaQuandoNaoHouverPendencias()
    {
        var importacao = CriarImportacaoComItem();
        var item = importacao.Itens.Single();

        item.Confirmar("Confirmado pelo operador");
        importacao.AtualizarStatusRevisao();

        importacao.Status.Should().Be(StatusImportacaoWhatsapp.Confirmado);
        importacao.ConfirmadoEmUtc.Should().NotBeNull();
        importacao.RejeitadoEmUtc.Should().BeNull();
    }

    [Fact]
    public void AtualizarStatusRevisao_DeveMarcarImportacaoComoRejeitadaQuandoTodosOsItensForemRejeitados()
    {
        var importacao = CriarImportacaoComItem();
        var item = importacao.Itens.Single();

        item.Rejeitar("Nao corresponde ao comprovante recebido");
        importacao.AtualizarStatusRevisao();

        importacao.Status.Should().Be(StatusImportacaoWhatsapp.Rejeitado);
        importacao.RejeitadoEmUtc.Should().NotBeNull();
        importacao.ConfirmadoEmUtc.Should().BeNull();
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
                """{"descricao":"Pix","valor":80.00}""")
        ]);

        return importacao;
    }
}
