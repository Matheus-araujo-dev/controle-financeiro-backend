using ControleFinanceiro.Domain.PlanejamentoCompras;
using FluentAssertions;

namespace ControleFinanceiro.Domain.Tests.PlanejamentoCompras;

public sealed class PlanejamentoCompraTests
{
    [Fact]
    public void Criar_QuandoValorEstimadoNaoForPositivo_DeveFalhar()
    {
        var action = () => PlanejamentoCompra.Criar(
            "Notebook novo",
            "Compra planejada",
            0m,
            new DateOnly(2026, 5, 10),
            PrioridadePlanejamentoCompra.Alta,
            StatusPlanejamentoCompra.Planejada,
            true,
            6,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "Aguardar promocao");

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("valorEstimado");
    }

    [Fact]
    public void Criar_QuandoQuantidadeParcelasInformadaForMenorQueDois_DeveFalhar()
    {
        var action = () => PlanejamentoCompra.Criar(
            "Notebook novo",
            "Compra planejada",
            3500m,
            new DateOnly(2026, 5, 10),
            PrioridadePlanejamentoCompra.Alta,
            StatusPlanejamentoCompra.Planejada,
            true,
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "Aguardar promocao");

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("quantidadeParcelasDesejada");
    }

    [Fact]
    public void Criar_QuandoPayloadValido_DeveNormalizarCampos()
    {
        var contaGerencialId = Guid.NewGuid();
        var responsavelId = Guid.NewGuid();

        var planejamento = PlanejamentoCompra.Criar(
            "  Notebook novo  ",
            "  Troca do equipamento principal  ",
            3500.129m,
            new DateOnly(2026, 5, 10),
            PrioridadePlanejamentoCompra.Alta,
            StatusPlanejamentoCompra.Planejada,
            false,
            6,
            contaGerencialId,
            responsavelId,
            "  https://loja.exemplo.com/produto/notebook  ",
            "  Comprar na Black Friday  ");

        planejamento.Titulo.Should().Be("Notebook novo");
        planejamento.Descricao.Should().Be("Troca do equipamento principal");
        planejamento.ValorEstimado.Should().Be(3500.13m);
        planejamento.Parcelavel.Should().BeFalse();
        planejamento.QuantidadeParcelasDesejada.Should().BeNull();
        planejamento.ContaGerencialId.Should().Be(contaGerencialId);
        planejamento.ResponsavelId.Should().Be(responsavelId);
        planejamento.Link.Should().Be("https://loja.exemplo.com/produto/notebook");
        planejamento.Observacao.Should().Be("Comprar na Black Friday");
    }

    [Fact]
    public void Criar_QuandoLinkNaoForAbsoluto_DeveFalhar()
    {
        var action = () => PlanejamentoCompra.Criar(
            "Notebook novo",
            "Compra planejada",
            3500m,
            new DateOnly(2026, 5, 10),
            PrioridadePlanejamentoCompra.Alta,
            StatusPlanejamentoCompra.Planejada,
            true,
            6,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "produto/notebook",
            "Aguardar promocao");

        action.Should().Throw<ArgumentException>()
            .WithParameterName("link");
    }
}
