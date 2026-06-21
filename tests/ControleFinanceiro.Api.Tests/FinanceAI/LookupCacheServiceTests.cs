using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Common.Cache;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Api.Tests.FinanceAI;

public sealed class LookupCacheServiceTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Lookups_DeveCarregarPorTipoEPorId()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ILookupCacheService>();
        var ct = CancellationToken.None;

        var statusContas = await cache.GetAllStatusContaAsync(ct);
        statusContas.Should().NotBeEmpty();
        (await cache.GetStatusContaByIdAsync(statusContas[0].Id, ct)).Should().NotBeNull();

        var statusMov = await cache.GetAllStatusMovimentacaoAsync(ct);
        statusMov.Should().NotBeEmpty();
        (await cache.GetStatusMovimentacaoByIdAsync(statusMov[0].Id, ct)).Should().NotBeNull();

        var formas = await cache.GetAllFormaPagamentoAsync(ct);
        formas.Should().NotBeEmpty();
        (await cache.GetFormaPagamentoByIdAsync(formas[0].Id, ct)).Should().NotBeNull();

        var contasGerenciais = await cache.GetAllContaGerencialAsync(ct);
        contasGerenciais.Should().NotBeEmpty();
        (await cache.GetContaGerencialByIdAsync(contasGerenciais[0].Id, ct)).Should().NotBeNull();
    }

    [Fact]
    public async Task GetById_ComIdInexistente_DeveRetornarNull()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ILookupCacheService>();
        var ct = CancellationToken.None;

        (await cache.GetStatusContaByIdAsync(Guid.NewGuid(), ct)).Should().BeNull();
        (await cache.GetFormaPagamentoByIdAsync(Guid.NewGuid(), ct)).Should().BeNull();
        (await cache.GetContaGerencialByIdAsync(Guid.NewGuid(), ct)).Should().BeNull();
        (await cache.GetStatusMovimentacaoByIdAsync(Guid.NewGuid(), ct)).Should().BeNull();
    }

    [Fact]
    public async Task Refresh_DeveInvalidarERecarregar()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ILookupCacheService>();
        var ct = CancellationToken.None;

        // Popula o cache
        await cache.GetAllStatusContaAsync(ct);
        await cache.GetAllFormaPagamentoAsync(ct);
        await cache.GetAllContaGerencialAsync(ct);
        await cache.GetAllStatusMovimentacaoAsync(ct);

        // Invalida tudo e recarrega
        await cache.RefreshAllAsync(ct);
        await cache.RefreshStatusContaAsync(ct);
        await cache.RefreshFormaPagamentoAsync(ct);
        await cache.RefreshContaGerencialAsync(ct);
        await cache.RefreshStatusMovimentacaoAsync(ct);

        (await cache.GetAllFormaPagamentoAsync(ct)).Should().NotBeEmpty();
    }
}
