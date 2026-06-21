using ControleFinanceiro.Application.Common.FeatureFlags;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Application.Tests.Common.FeatureFlags;

public sealed class InMemoryFeatureFlagServiceTests
{
    private static InMemoryFeatureFlagService Criar(params (string Nome, bool Habilitado, string Variante)[] flags)
    {
        var options = new FeatureFlagsOptions();
        foreach (var (nome, habilitado, variante) in flags)
        {
            options.Flags[nome] = new FeatureFlagConfig { Enabled = habilitado, Variant = variante };
        }

        return new InMemoryFeatureFlagService(Options.Create(options), NullLogger<InMemoryFeatureFlagService>.Instance);
    }

    [Fact]
    public void IsEnabled_FlagHabilitada_DeveRetornarTrue()
    {
        var servico = Criar(("recurso", true, "control"));

        servico.IsEnabled("recurso").Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_FlagDesabilitada_DeveRetornarFalse()
    {
        var servico = Criar(("recurso", false, "control"));

        servico.IsEnabled("recurso").Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_FlagInexistente_DeveRetornarFalse()
    {
        var servico = Criar();

        servico.IsEnabled("ausente").Should().BeFalse();
    }

    [Fact]
    public void GetVariant_DeveRetornarVarianteConfiguradaOuDefault()
    {
        var servico = Criar(("recurso", true, "tratamento"));

        servico.GetVariant("recurso").Should().Be("tratamento");
        servico.GetVariant("ausente").Should().Be("control");
        servico.GetVariant("ausente", "fallback").Should().Be("fallback");
    }

    [Fact]
    public async Task EvaluateAsync_DeveSeguirIsEnabled()
    {
        var servico = Criar(("recurso", true, "control"));

        (await servico.EvaluateAsync("recurso")).Should().BeTrue();
        (await servico.EvaluateAsync("ausente")).Should().BeFalse();
    }
}
