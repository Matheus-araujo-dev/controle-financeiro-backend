using ControleFinanceiro.Api.Configuration;
using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.FinanceAI;
using ControleFinanceiro.SharedKernel.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Api.Tests.FinanceAI;

public sealed class FinanceInsightsServiceTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private sealed class FakeCurrentUser(Guid? familiaId) : ICurrentUser
    {
        public bool IsAuthenticated => familiaId is not null;
        public string? UserId => Guid.NewGuid().ToString();
        public Guid? WorkspaceId => familiaId;
        public Guid? FamiliaId => WorkspaceId;
        public string? Papel => "Administrador";
    }

    private FinanceInsightsService Criar(IServiceScope scope, Guid? familiaId, IMemoryCache cache) =>
        new(
            scope.ServiceProvider.GetRequiredService<ILlmClient>(),
            scope.ServiceProvider.GetRequiredService<IAppDbContext>(),
            new FakeCurrentUser(familiaId),
            cache,
            NullLogger<FinanceInsightsService>.Instance);

    [SqlServerFact]
    public async Task GerarInsightsAsync_ComFamiliaEDados_DeveRetornarResposta()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);
        var familiaId = _factory.Services.GetRequiredService<IOptions<AuthOptions>>().Value.DevelopmentFamiliaId;

        using var scope = _factory.Services.CreateScope();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var servico = Criar(scope, familiaId, cache);

        var resposta = await servico.GerarInsightsAsync("2026-04", CancellationToken.None);

        resposta.Should().NotBeNull();
        resposta.Insights.Should().NotBeNull();
    }

    [SqlServerFact]
    public async Task GerarInsightsAsync_SegundaChamada_DeveUsarCache()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);
        var familiaId = _factory.Services.GetRequiredService<IOptions<AuthOptions>>().Value.DevelopmentFamiliaId;

        using var scope = _factory.Services.CreateScope();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var servico = Criar(scope, familiaId, cache);

        var primeira = await servico.GerarInsightsAsync("2026-05", CancellationToken.None);
        var segunda = await servico.GerarInsightsAsync("2026-05", CancellationToken.None);

        segunda.Should().BeSameAs(primeira);
    }

    [Fact]
    public async Task GerarInsightsAsync_SemFamilia_DeveLancar()
    {
        using var scope = _factory.Services.CreateScope();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var servico = Criar(scope, familiaId: null, cache);

        var acao = async () => await servico.GerarInsightsAsync("2026-04", CancellationToken.None);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }
}


