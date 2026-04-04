using ControleFinanceiro.Domain.Cadastros.Pessoas;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Cadastros;

public sealed class PessoaTests
{
    [Fact]
    public void Criar_QuandoNomeNaoInformado_DeveFalhar()
    {
        var action = () => Pessoa.Criar(" ", TipoPessoa.Fisica, null, null, null, null, true);

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
            true);

        pessoa.CpfCnpj.Should().Be("12345678000190");
    }

    [Fact]
    public void Inativar_DeveMarcarCadastroComoInativo()
    {
        var pessoa = Pessoa.Criar("Cliente Exemplo", TipoPessoa.Fisica, null, null, null, null, true);

        pessoa.Inativar();

        pessoa.Ativo.Should().BeFalse();
    }
}
