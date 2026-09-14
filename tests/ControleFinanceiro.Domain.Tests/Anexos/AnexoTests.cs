using ControleFinanceiro.Domain.Anexos;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Anexos;

public sealed class AnexoTests
{
    [Fact]
    public void Criar_QuandoPayloadValido_DeveCriarAnexo()
    {
        var anexo = Anexo.Criar(
            "comprovante.pdf",
            "/path/comprovante.pdf",
            "application/pdf",
            12345,
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            OrigemAnexo.Manual,
            null, null);

        anexo.NomeArquivoOriginal.Should().Be("comprovante.pdf");
        anexo.CaminhoArquivo.Should().Be("/path/comprovante.pdf");
        anexo.MimeType.Should().Be("application/pdf");
        anexo.TamanhoBytes.Should().Be(12345);
        anexo.HashSha256.Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
        anexo.Origem.Should().Be(OrigemAnexo.Manual);
    }

    [Fact]
    public void Criar_QuandoNomeArquivoVazio_DeveFalhar()
    {
        var action = () => Anexo.Criar("", "/path/file.pdf", "application/pdf", 100, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            OrigemAnexo.Manual, null, null);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Vincular_DeveAdicionarVinculo()
    {
        var anexo = Anexo.Criar("file.pdf", "/path/file.pdf", "application/pdf", 100, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            OrigemAnexo.Manual, null, null);
        var entidadeId = Guid.NewGuid();

        anexo.Vincular(TipoEntidadeAnexo.ContaPagar, entidadeId);

        anexo.Vinculos.Should().HaveCount(1);
        anexo.Vinculos.First().TipoEntidade.Should().Be(TipoEntidadeAnexo.ContaPagar);
        anexo.Vinculos.First().EntidadeId.Should().Be(entidadeId);
    }
}