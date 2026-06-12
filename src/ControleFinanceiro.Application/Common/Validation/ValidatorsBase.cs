using FluentValidation;

namespace ControleFinanceiro.Application.Common.Validation;

public abstract class DomainValidator<T> : AbstractValidator<T> { }

public static class ValidatorExtensions
{
    public static IRuleBuilderOptions<T, string> MustBeValidDocumento<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .MaximumLength(50)
            .WithMessage("Número do documento deve ter no máximo 50 caracteres.");
    }

    public static IRuleBuilderOptions<T, decimal> MustBePositiveMoney<T>(this IRuleBuilder<T, decimal> ruleBuilder, string fieldName = "Valor")
    {
        return ruleBuilder
            .GreaterThan(0)
            .WithMessage($"{fieldName} deve ser maior que zero.")
            .PrecisionScale(18, 2, true)
            .WithMessage($"{fieldName} deve ter no máximo 18 dígitos e 2 casas decimais.");
    }

    public static IRuleBuilderOptions<T, DateOnly> MustBeValidDate<T>(this IRuleBuilder<T, DateOnly> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage("Data é obrigatória.");
    }

    public static IRuleBuilderOptions<T, Guid> MustNotBeEmpty<T>(this IRuleBuilder<T, Guid> ruleBuilder, string fieldName)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage($"{fieldName} é obrigatório.");
    }
}