using ControleFinanceiro.Application.Common.Exceptions;

namespace ControleFinanceiro.Application.Common.Validation;

public static class ValidationExceptionFactory
{
    public static ApplicationValidationException Create(string field, string message)
    {
        return new ApplicationValidationException(
            "Um ou mais campos sao invalidos.",
            new Dictionary<string, string[]>
            {
                [field] = [message]
            });
    }
}
