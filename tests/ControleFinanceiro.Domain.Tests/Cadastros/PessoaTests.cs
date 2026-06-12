using ControleFinanceiro.Domain.Cadastros.Pessoas;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Cadastros;

public sealed class PessoaTests
{
    [Fact]
    public void Criar_QuandoNomeNaoInformado_DeveFalhar()
    {
        var action = () => Pessoa.Criar(" ", TipoPessoa.Fisica, null, null, null, null, [], true);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("nome");
    }

    [Fact]
    public void Criar_QuandoDocumentoInformado_DeveNormalizarDigitos()
    {
        var pessoa = Pessoa.Criar(
            "Fornecedor Exemplo",
            TipoPessoa.Juridica,
            "12.345.678/0001-90",
            "financeiro@example.com",
            "11999999999",
            "Observacao",
            [],
            true);

        pessoa.CpfCnpj.Should().Be("12345678000190");
    }

    [Fact]
    public void Inativar_DeveMarcarCadastroComoInativo()
    {
        var pessoa = Pessoa.Criar("Cliente Exemplo", TipoPessoa.Fisica, null, null, null, null, [], true);

        pessoa.Inativar();

        pessoa.Ativo.Should().BeFalse();
    }

    [Fact]
    public void Criar_QuandoChavesPixInformadas_DeveNormalizarEManterColecao()
    {
        var pessoa = Pessoa.Criar(
            "Cliente Exemplo",
            TipoPessoa.Fisica,
            null,
            null,
            null,
            null,
            [
                ChavePixPlano.Create(TipoChavePix.CpfCnpj, "437.782.098-25"),
                ChavePixPlano.Create(TipoChavePix.Email, "Pix@Example.com "),
                ChavePixPlano.Create(TipoChavePix.Telefone, "(11) 98889-1273")
            ],
            true);

        pessoa.ChavesPix.Should().HaveCount(3);
        pessoa.ChavesPix.Select(item => item.Chave).Should().ContainInOrder("43778209825", "pix@example.com", "11988891273");
    }

    [Fact]
    public void Atualizar_QuandoChavesPixDuplicadas_DeveFalhar()
    {
        var pessoa = Pessoa.Criar("Cliente Exemplo", TipoPessoa.Fisica, null, null, null, null, [], true);

        var action = () => pessoa.Atualizar(
            "Cliente Exemplo",
            TipoPessoa.Fisica,
            null,
            null,
            null,
            null,
            [
                ChavePixPlano.Create(TipoChavePix.Email, "pix@example.com"),
                ChavePixPlano.Create(TipoChavePix.Email, " PIX@example.com ")
            ],
            true);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*duplicada*")
            .WithParameterName("chavesPix");
    }
}
