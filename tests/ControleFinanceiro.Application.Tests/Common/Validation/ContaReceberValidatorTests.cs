using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasReceber;
using FluentAssertions;
using FluentValidation;

namespace ControleFinanceiro.Application.Tests.Common.Validation;

public sealed class ContaReceberValidatorTests
{
    private static CriarContaReceberRequest ValidaCriacao() => new(
        NumeroDocumento: "REC-1",
        DataEmissao: new DateOnly(2026, 6, 1),
        ResponsavelId: null,
        PagadorId: Guid.NewGuid(),
        DataVencimento: new DateOnly(2026, 6, 10),
        FormaPagamentoId: Guid.NewGuid(),
        CartaoId: null,
        ContaBancariaId: null,
        DataLiquidacao: null,
        ValorOriginal: 100m,
        ValorDesconto: 0m,
        ValorJuros: 0m,
        ValorMulta: 0m,
        QuantidadeParcelas: 1,
        Descricao: "Receita teste",
        Observacao: null,
        Rateios: [new RateioRequest(Guid.NewGuid(), 100m)],
        Recorrencia: null);

    private static readonly CreateContaReceberValidator Create = new();

    [Fact]
    public void Create_RequestValido_DevePassar()
    {
        Create.Validate(ValidaCriacao()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_CamposObrigatoriosVazios_DeveAcumularErros()
    {
        var req = ValidaCriacao() with
        {
            DataEmissao = default,
            PagadorId = Guid.Empty,
            DataVencimento = default,
            FormaPagamentoId = Guid.Empty,
            Descricao = "",
            Rateios = []
        };

        var erros = Create.Validate(req).Errors.Select(e => e.PropertyName).ToArray();
        erros.Should().Contain(["DataEmissao", "PagadorId", "DataVencimento", "FormaPagamentoId", "Descricao", "Rateios"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_ValorOriginalInvalido_DeveFalhar(decimal valor)
    {
        Create.Validate(ValidaCriacao() with { ValorOriginal = valor }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(361)]
    public void Create_QuantidadeParcelasForaDaFaixa_DeveFalhar(int parcelas)
    {
        Create.Validate(ValidaCriacao() with { QuantidadeParcelas = parcelas }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_ValoresNegativosEDescricaoLonga_DeveFalhar()
    {
        var req = ValidaCriacao() with
        {
            ValorDesconto = -1m,
            ValorJuros = -1m,
            ValorMulta = -1m,
            Descricao = new string('a', 201),
            Observacao = new string('b', 501)
        };

        var erros = Create.Validate(req).Errors.Select(e => e.PropertyName).ToArray();
        erros.Should().Contain(["ValorDesconto", "ValorJuros", "ValorMulta", "Descricao", "Observacao"]);
    }

    [Fact]
    public void Update_SemId_DeveFalhar_EComIdValidoPassar()
    {
        var validator = new UpdateContaReceberValidator();
        var valido = new AtualizarContaReceberRequest(
            Id: Guid.NewGuid(),
            NumeroDocumento: "REC-1",
            DataEmissao: new DateOnly(2026, 6, 1),
            ResponsavelId: null,
            PagadorId: Guid.NewGuid(),
            DataVencimento: new DateOnly(2026, 6, 10),
            FormaPagamentoId: Guid.NewGuid(),
            CartaoId: null,
            ContaBancariaId: null,
            DataLiquidacao: null,
            ValorOriginal: 100m,
            ValorDesconto: 0m,
            ValorJuros: 0m,
            ValorMulta: 0m,
            QuantidadeParcelas: 1,
            Descricao: "Receita",
            Observacao: null,
            Rateios: [new RateioRequest(Guid.NewGuid(), 100m)],
            Recorrencia: null);

        validator.Validate(valido).IsValid.Should().BeTrue();
        validator.Validate(valido with { Id = Guid.Empty }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(0, false)]
    public void Liquidar_ValorLiquidacao_DeveValidar(decimal valor, bool esperaValido)
    {
        var validator = new LiquidarContaReceberValidator();
        var req = new LiquidarContaReceberRequest(valor, new DateOnly(2026, 6, 10), Guid.NewGuid());

        validator.Validate(req).IsValid.Should().Be(esperaValido);
    }

    [Fact]
    public void Liquidar_SemContaBancariaOuData_DeveFalhar()
    {
        var validator = new LiquidarContaReceberValidator();
        var req = new LiquidarContaReceberRequest(100m, default, Guid.Empty);

        var erros = validator.Validate(req).Errors.Select(e => e.PropertyName).ToArray();
        erros.Should().Contain(["DataLiquidacao", "ContaBancariaId"]);
    }
}
