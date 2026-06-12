using ControleFinanceiro.Application.Common.Exceptions;

namespace ControleFinanceiro.Application.Common.Validation;

public interface IValidationResultFactory
{
    ApplicationValidationException Create(string field, string message);
}