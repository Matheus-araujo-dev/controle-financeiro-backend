using ControleFinanceiro.Application.Common.Exceptions;

namespace ControleFinanceiro.Application.Common.Validation;

public class ValidationResultFactory : IValidationResultFactory
{
    public ApplicationValidationException Create(string field, string message)
    {
        return new ApplicationValidationException(
            "Um ou mais campos são inválidos.",
            new Dictionary<string, string[]>
            {
                [field] = [message]
            });
    }
}