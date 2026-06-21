using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;
using FluentAssertions;
using FluentValidation;

namespace ControleFinanceiro.Application.Tests.Common.Validation;

public sealed class ContaPagarValidatorTests
{
    private static CriarContaPagarRequest ValidaCriacao() => new(
        OrigemCompraPlanejadaId: null,
        NumeroDocumento: "DOC-1",
        DataEmissao: new DateOnly(2026, 6, 1),
        ResponsavelCompraId: null,
        RecebedorId: Guid.NewGuid(),
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
        Descricao: "Conta teste",
        Observacao: null,
        Rateios: [new RateioRequest(Guid.NewGuid(), 100m)],
        Recorrencia: null);

    private static readonly CreateContaPagarValidator Create = new();

    [Fact]
    public void Create_RequestValido_DevePassar()
    {
        Create.Validate(ValidaCriacao()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_ValorOriginalInvalido_DeveFalhar(decimal valor)
    {
        var req = ValidaCriacao() with { ValorOriginal = valor, Rateios = [new RateioRequest(Guid.NewGuid(), valor)] };

        Create.Validate(req).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_CamposObrigatoriosVazios_DeveAcumularErros()
    {
        var req = ValidaCriacao() with
        {
            DataEmissao = default,
            RecebedorId = Guid.Empty,
            DataVencimento = default,
            FormaPagamentoId = Guid.Empty,
            Descricao = "",
            Rateios = []
        };

        var resultado = Create.Validate(req);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Select(e => e.PropertyName).Should().Contain(
            ["DataEmissao", "RecebedorId", "DataVencimento", "FormaPagamentoId", "Descricao", "Rateios"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(361)]
    public void Create_QuantidadeParcelasForaDaFaixa_DeveFalhar(int parcelas)
    {
        var req = ValidaCriacao() with { QuantidadeParcelas = parcelas };

        Create.Validate(req).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_ValoresNegativos_DeveFalhar()
    {
        var req = ValidaCriacao() with { ValorDesconto = -1m, ValorJuros = -1m, ValorMulta = -1m };

        var erros = Create.Validate(req).Errors.Select(e => e.PropertyName).ToArray();
        erros.Should().Contain(["ValorDesconto", "ValorJuros", "ValorMulta"]);
    }

    [Fact]
    public void Create_DescricaoEObservacaoMuitoLongas_DeveFalhar()
    {
        var req = ValidaCriacao() with { Descricao = new string('a', 201), Observacao = new string('b', 501) };

        var erros = Create.Validate(req).Errors.Select(e => e.PropertyName).ToArray();
        erros.Should().Contain(["Descricao", "Observacao"]);
    }

    [Fact]
    public void Create_SomaDeRateiosNaoFechaValorLiquido_DeveFalhar()
    {
        var req = ValidaCriacao() with
        {
            ValorOriginal = 100m,
            Rateios = [new RateioRequest(Guid.NewGuid(), 80m)]
        };

        Create.Validate(req).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_SomaDeRateiosConsideraDescontoJurosMulta()
    {
        // líquido = 100 - 10 + 5 + 5 = 100
        var req = ValidaCriacao() with
        {
            ValorOriginal = 100m,
            ValorDesconto = 10m,
            ValorJuros = 5m,
            ValorMulta = 5m,
            Rateios = [new RateioRequest(Guid.NewGuid(), 100m)]
        };

        Create.Validate(req).IsValid.Should().BeTrue();
    }

    [Fact]
    public void RateioRequestValidator_ContaGerencialVaziaOuValorInvalido_DeveFalhar()
    {
        var req = ValidaCriacao() with { Rateios = [new RateioRequest(Guid.Empty, 0m)] };

        var erros = Create.Validate(req).Errors.Select(e => e.PropertyName).ToArray();
        erros.Should().Contain(e => e.Contains("ContaGerencialId"));
        erros.Should().Contain(e => e.Contains("Valor"));
    }

    [Fact]
    public void Update_SemId_DeveFalhar_EComIdValidoPassar()
    {
        var validator = new UpdateContaPagarValidator();
        var valido = new AtualizarContaPagarRequest(
            Id: Guid.NewGuid(),
            NumeroDocumento: "DOC-1",
            DataEmissao: new DateOnly(2026, 6, 1),
            ResponsavelCompraId: null,
            RecebedorId: Guid.NewGuid(),
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
            Descricao: "Conta",
            Observacao: null,
            Rateios: [new RateioRequest(Guid.NewGuid(), 100m)],
            Recorrencia: null);

        validator.Validate(valido).IsValid.Should().BeTrue();
        validator.Validate(valido with { Id = Guid.Empty }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void Liquidar_ValorLiquidacao_DeveValidar(decimal valor, bool esperaValido)
    {
        var validator = new LiquidarContaPagarValidator();
        var req = new LiquidarContaPagarRequest(valor, new DateOnly(2026, 6, 10), Guid.NewGuid());

        validator.Validate(req).IsValid.Should().Be(esperaValido);
    }

    [Fact]
    public void Liquidar_SemContaBancariaOuData_DeveFalhar()
    {
        var validator = new LiquidarContaPagarValidator();
        var req = new LiquidarContaPagarRequest(100m, default, Guid.Empty);

        var erros = validator.Validate(req).Errors.Select(e => e.PropertyName).ToArray();
        erros.Should().Contain(["DataLiquidacao", "ContaBancariaId"]);
    }
}
