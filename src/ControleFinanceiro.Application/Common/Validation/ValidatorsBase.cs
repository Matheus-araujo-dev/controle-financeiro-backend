using FluentValidation;

namespace ControleFinanceiro.Application.Common.Validation;

public abstract class DomainValidator<T> : AbstractValidator<T> { }