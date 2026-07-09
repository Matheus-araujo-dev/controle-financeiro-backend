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

    [Fact]
    public void Criar_ComIconeECor_DeveArmazenarValoresNormalizados()
    {
        var conta = ContaBancaria.Criar(
            "Nubank",
            "Nubank",
            null,
            null,
            "Digital",
            0m,
            new DateOnly(2026, 1, 1),
            null,
            true,
            icone: "  account_balance  ",
            cor: "  #8b5cf6  ");

        conta.Icone.Should().Be("account_balance");
        conta.Cor.Should().Be("#8b5cf6");
    }

    [Fact]
    public void Criar_SemIconeECor_DeveManterNull()
    {
        var conta = ContaBancaria.Criar(
            "Bradesco",
            "Bradesco",
            null,
            null,
            "Corrente",
            0m,
            new DateOnly(2026, 1, 1),
            null,
            true);

        conta.Icone.Should().BeNull();
        conta.Cor.Should().BeNull();
    }
}
