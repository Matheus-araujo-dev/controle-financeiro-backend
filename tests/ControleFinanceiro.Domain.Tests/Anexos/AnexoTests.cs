using ControleFinanceiro.Domain.Anexos;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Anexos;

public sealed class AnexoTests
{
    [Fact]
    public void Criar_DeveNormalizarMetadadosEVincularMultiplasEntidades()
    {
        var anexo = Anexo.Criar(
            "  comprovante pix.pdf  ",
            "App_Data/anexos/arquivo.pdf",
            "application/pdf",
            128,
            new string('a', 64),
            OrigemAnexo.Manual,
            null,
            null);

        var contaPagarId = Guid.NewGuid();
        var faturaId = Guid.NewGuid();

        anexo.Vincular(TipoEntidadeAnexo.ContaPagar, contaPagarId);
        anexo.Vincular(TipoEntidadeAnexo.FaturaCartao, faturaId);
        anexo.Vincular(TipoEntidadeAnexo.ContaPagar, contaPagarId);

        anexo.NomeArquivoOriginal.Should().Be("comprovante pix.pdf");
        anexo.Vinculos.Should().HaveCount(2);
        anexo.Vinculos.Should().Contain(x => x.TipoEntidade == TipoEntidadeAnexo.ContaPagar && x.EntidadeId == contaPagarId);
        anexo.Vinculos.Should().Contain(x => x.TipoEntidade == TipoEntidadeAnexo.FaturaCartao && x.EntidadeId == faturaId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemNome_DeveFalhar(string nome)
    {
        var action = () => Anexo.Criar(
            nome,
            "App_Data/anexos/arquivo.pdf",
            "application/pdf",
            128,
            new string('a', 64),
            OrigemAnexo.Manual,
            null,
            null);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Vincular_ComEntidadeVazia_DeveFalhar()
    {
        var anexo = Anexo.Criar(
            "comprovante.pdf",
            "App_Data/anexos/arquivo.pdf",
            "application/pdf",
            128,
            new string('a', 64),
            OrigemAnexo.Manual,
            null,
            null);

        var action = () => anexo.Vincular(TipoEntidadeAnexo.ContaPagar, Guid.Empty);

        action.Should().Throw<ArgumentException>();
    }
}
