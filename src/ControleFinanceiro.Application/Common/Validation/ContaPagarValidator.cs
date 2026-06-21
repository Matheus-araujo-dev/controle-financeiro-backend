using ControleFinanceiro.Contracts.Financeiro.ContasPagar;
using ControleFinanceiro.Contracts.Financeiro.Common;
using FluentValidation;

namespace ControleFinanceiro.Application.Common.Validation;

public class CreateContaPagarValidator : DomainValidator<CriarContaPagarRequest>
{
    public CreateContaPagarValidator()
    {
        RuleFor(x => x.NumeroDocumento)
            .MaximumLength(50)
            .WithMessage("Número do documento deve ter no máximo 50 caracteres.");

        RuleFor(x => x.DataEmissao)
            .NotEmpty()
            .WithMessage("Data de emissão é obrigatória.");

        RuleFor(x => x.RecebedorId)
            .NotEmpty()
            .WithMessage("Recebedor é obrigatório.");

        RuleFor(x => x.DataVencimento)
            .NotEmpty()
            .WithMessage("Data de vencimento é obrigatória.");

        RuleFor(x => x.FormaPagamentoId)
            .NotEmpty()
            .WithMessage("Forma de pagamento é obrigatória.");

        RuleFor(x => x.ValorOriginal)
            .GreaterThan(0)
            .WithMessage("Valor original deve ser maior que zero.")
            .PrecisionScale(18, 2, true)
            .WithMessage("Valor original deve ter no máximo 18 dígitos e 2 casas decimais.");

        RuleFor(x => x.ValorDesconto)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Valor desconto não pode ser negativo.");

        RuleFor(x => x.ValorJuros)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Valor juros não pode ser negativo.");

        RuleFor(x => x.ValorMulta)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Valor multa não pode ser negativo.");

        RuleFor(x => x.QuantidadeParcelas)
            .InclusiveBetween(1, 360)
            .WithMessage("Quantidade de parcelas deve estar entre 1 e 360.");

        RuleFor(x => x.Descricao)
            .NotEmpty()
            .WithMessage("Descrição é obrigatória.")
            .MaximumLength(200)
            .WithMessage("Descrição deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Observacao)
            .MaximumLength(500)
            .WithMessage("Observação deve ter no máximo 500 caracteres.");

        RuleFor(x => x.Rateios)
            .NotEmpty()
            .WithMessage("Ao menos um rateio é obrigatório.");

        RuleForEach(x => x.Rateios).SetValidator(new RateioRequestValidator());

        RuleFor(x => x)
            .Must((request) => ValidateRateiosSum(request))
            .WithMessage("A soma dos rateios deve fechar exatamente o valor líquido.");
    }

    private static bool ValidateRateiosSum(CriarContaPagarRequest request)
    {
        if (request.Rateios == null || request.Rateios.Count == 0)
            return false;

        var valorLiquido = request.ValorOriginal - request.ValorDesconto + request.ValorJuros + request.ValorMulta;
        var somaRateios = request.Rateios.Sum(r => r.Valor);
        
        return Math.Round(somaRateios, 2) == Math.Round(valorLiquido, 2);
    }
}

public class RateioRequestValidator : DomainValidator<RateioRequest>
{
    public RateioRequestValidator()
    {
        RuleFor(x => x.ContaGerencialId)
            .NotEmpty()
            .WithMessage("Conta gerencial é obrigatória.");

        RuleFor(x => x.Valor)
            .GreaterThan(0)
            .WithMessage("Valor do rateio deve ser maior que zero.");
    }
}

public class UpdateContaPagarValidator : DomainValidator<AtualizarContaPagarRequest>
{
    public UpdateContaPagarValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("ID é obrigatório.");

        RuleFor(x => x.NumeroDocumento)
            .MaximumLength(50)
            .WithMessage("Número do documento deve ter no máximo 50 caracteres.");

        RuleFor(x => x.DataEmissao)
            .NotEmpty()
            .WithMessage("Data de emissão é obrigatória.");

        RuleFor(x => x.RecebedorId)
            .NotEmpty()
            .WithMessage("Recebedor é obrigatório.");

        RuleFor(x => x.DataVencimento)
            .NotEmpty()
            .WithMessage("Data de vencimento é obrigatória.");

        RuleFor(x => x.FormaPagamentoId)
            .NotEmpty()
            .WithMessage("Forma de pagamento é obrigatória.");

        RuleFor(x => x.ValorOriginal)
            .GreaterThan(0)
            .WithMessage("Valor original deve ser maior que zero.");

        RuleFor(x => x.ValorDesconto)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Valor desconto não pode ser negativo.");

        RuleFor(x => x.ValorJuros)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Valor juros não pode ser negativo.");

        RuleFor(x => x.ValorMulta)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Valor multa não pode ser negativo.");

        RuleFor(x => x.QuantidadeParcelas)
            .InclusiveBetween(1, 360)
            .WithMessage("Quantidade de parcelas deve estar entre 1 e 360.");

        RuleFor(x => x.Descricao)
            .NotEmpty()
            .WithMessage("Descrição é obrigatória.")
            .MaximumLength(200)
            .WithMessage("Descrição deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Observacao)
            .MaximumLength(500)
            .WithMessage("Observação deve ter no máximo 500 caracteres.");

        RuleFor(x => x.Rateios)
            .NotEmpty()
            .WithMessage("Ao menos um rateio é obrigatório.");

        RuleForEach(x => x.Rateios).SetValidator(new RateioRequestValidator());
    }
}

public class LiquidarContaPagarValidator : DomainValidator<LiquidarContaPagarRequest>
{
    public LiquidarContaPagarValidator()
    {
        RuleFor(x => x.ValorLiquidacao)
            .GreaterThan(0)
            .WithMessage("Valor da liquidação deve ser maior que zero.")
            .PrecisionScale(18, 2, true)
            .WithMessage("Valor da liquidação deve ter no máximo 18 dígitos e 2 casas decimais.");

        RuleFor(x => x.DataLiquidacao)
            .NotEmpty()
            .WithMessage("Data de liquidação é obrigatória.");

        RuleFor(x => x.ContaBancariaId)
            .NotEmpty()
            .WithMessage("Conta bancária é obrigatória.");
    }
}
