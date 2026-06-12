using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.Cadastros;

public sealed class ContaBancariaTests
{
    [Fact]
    public void Criar_QuandoLimiteCompartilhadoForInformado_DeveArredondarValor()
    {
        var conta = ContaBancaria.Criar(
            "Conta principal",
            "Banco Exemplo",
            "0001",
            "12345-6",
            "Corrente",
            1500m,
            new DateOnly(2026, 4, 1),
            5000.129m,
            true);

        conta.LimiteCartoesCompartilhado.Should().Be(5000.13m);
    }

    [Fact]
    public void Criar_QuandoLimiteCompartilhadoForNegativo_DeveFalhar()
    {
        var action = () => ContaBancaria.Criar(
            "Conta principal",
            "Banco Exemplo",
            "0001",
            "12345-6",
            "Corrente",
            1500m,
            new DateOnly(2026, 4, 1),
            -1m,
            true);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("limiteCartoesCompartilhado");
    }
}
