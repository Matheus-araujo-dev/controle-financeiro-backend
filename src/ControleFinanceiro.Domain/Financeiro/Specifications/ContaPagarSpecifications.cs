using System.Linq.Expressions;
using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Financeiro.Specifications;

public sealed class ContaPagarPorVencimentoAteSpec(DateOnly dataFinal) : Specification<ContaPagar>
{
    public override Expression<Func<ContaPagar, bool>> ToExpression() =>
        conta => conta.DataVencimento <= dataFinal;
}

public sealed class ContaPagarPorVencimentoDeSpec(DateOnly dataInicial) : Specification<ContaPagar>
{
    public override Expression<Func<ContaPagar, bool>> ToExpression() =>
        conta => conta.DataVencimento >= dataInicial;
}

public sealed class ContaPagarPendentesSpec : Specification<ContaPagar>
{
    public override Expression<Func<ContaPagar, bool>> ToExpression() =>
        conta =>
            conta.StatusContaId != StatusConta.LiquidadaId &&
            conta.StatusContaId != StatusConta.CanceladaId;
}

public sealed class ContaPagarPorRecebedorSpec(Guid recebedorId) : Specification<ContaPagar>
{
    public override Expression<Func<ContaPagar, bool>> ToExpression() =>
        conta => conta.RecebedorId == recebedorId;
}

public sealed class ContaPagarNaoCartaoSpec : Specification<ContaPagar>
{
    public override Expression<Func<ContaPagar, bool>> ToExpression() =>
        conta => !conta.CartaoId.HasValue;
}

public sealed class ContaPagarPorValorMinimoSpec(decimal valorMinimo) : Specification<ContaPagar>
{
    public override Expression<Func<ContaPagar, bool>> ToExpression() =>
        conta => conta.ValorLiquido >= valorMinimo;
}

public sealed class ContaPagarPorValorMaximoSpec(decimal valorMaximo) : Specification<ContaPagar>
{
    public override Expression<Func<ContaPagar, bool>> ToExpression() =>
        conta => conta.ValorLiquido <= valorMaximo;
}
