using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Cadastros;

public sealed class ContaGerencialTests
{
    [Fact]
    public void AtualizarContaPai_QuandoContaPaiForAPropriaConta_DeveFalhar()
    {
        var conta = ContaGerencial.Criar("ADM", "Administrativo", TipoContaGerencial.Despesa, null, true);

        var action = () => conta.AtualizarContaPai(conta.Id);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("contaPaiId");
    }

    [Fact]
    public void Criar_DevePermitirContaPaiNula()
    {
        var conta = ContaGerencial.Criar(null, "Receitas", TipoContaGerencial.Receita, null, true);

        conta.ContaPaiId.Should().BeNull();
    }
}
