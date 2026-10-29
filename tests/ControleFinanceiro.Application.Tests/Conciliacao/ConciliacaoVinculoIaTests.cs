using ControleFinanceiro.Application.Conciliacao;
using ControleFinanceiro.Domain.Conciliacao;
using FluentAssertions;

namespace ControleFinanceiro.Application.Tests.Conciliacao;

public sealed class ConciliacaoVinculoIaTests
{
    [Theory]
    [InlineData(3, 3, 484.34, true)]
    [InlineData(2, 3, 484.34, false)]
    [InlineData(3, 3, -484.34, false)]
    public void Vinculo_ExigeContaPermitidaSinalEParcela(int parcela, int total, decimal valor, bool aceito)
    {
        var item = ItemConciliacao.CriarFatura(new DateOnly(2026,9,18), "Sb Fit", 484.33m, "key", 3, 3);
        var conta = new CompraComparavel(Guid.NewGuid(), new DateOnly(2026,7,7), "Meias Pilates", valor, parcela, total);
        var json = $$"""{"sugestoes":[{"itemId":"{{item.Id}}","contaPagarId":"{{conta.Id}}","confianca":0.95,"justificativa":"Mesma compra parcelada, diferença de um centavo"}]}""";
        var result = ConciliacaoSugestaoService.ParseSugestoes(json, [item], new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>(), [conta]);
        result.Sugestoes.Single().ContaPagarId.Should().Be(aceito ? conta.Id : null);
        var semPermissao = ConciliacaoSugestaoService.ParseSugestoes(json, [item], new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>(), []);
        semPermissao.Sugestoes.Single().ContaPagarId.Should().BeNull();
    }
}
