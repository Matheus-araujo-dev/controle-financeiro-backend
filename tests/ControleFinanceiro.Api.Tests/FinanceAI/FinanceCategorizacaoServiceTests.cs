using ControleFinanceiro.Api.Configuration;
using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Api.Tests.Infrastructure;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.FinanceAI;
using ControleFinanceiro.SharedKernel.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ControleFinanceiro.Api.Tests.FinanceAI;

public sealed class FinanceCategorizacaoServiceTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    // ─── Fakes ────────────────────────────────────────────────────────────────

    private sealed class FakeLlm : ILlmClient
    {
        private string? _nextText;
        private bool _shouldThrow;

        public void EnqueueText(string text) => _nextText = text;
        public void EnqueueThrow() => _shouldThrow = true;

        public Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken ct)
        {
            if (_shouldThrow)
                throw new InvalidOperationException("LLM service unavailable");

            return Task.FromResult(new LlmCompletion(_nextText, [], new LlmUsage(5, 10), "end_turn"));
        }
    }

    private sealed class FakeCurrentUser(string? userId, Guid? familiaId) : ICurrentUser
    {
        public bool IsAuthenticated => userId is not null;
        public string? UserId => userId;
        public string? UserEmail => null;
        public Guid? WorkspaceId => familiaId;
        public Guid? FamiliaId => WorkspaceId;
        public string? Papel => "Administrador";
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private FinanceCategorizacaoService CriarServico(IAppDbContext db, FakeLlm llm, Guid? familiaId) =>
        new(llm, db, new FakeCurrentUser(Guid.NewGuid().ToString(), familiaId),
            NullLogger<FinanceCategorizacaoService>.Instance);

    // ─── Testes ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CategorizarAsync_ListaVazia_RetornaVazioSemChamarLlm()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var llm = new FakeLlm();

        var servico = CriarServico(db, llm, Guid.NewGuid());
        var resultado = await servico.CategorizarAsync([], CancellationToken.None);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task CategorizarAsync_SemCategoriasNoBanco_RetornaNullParaCategoria()
    {
        await _factory.ResetDatabaseAsync();
        // Familia sem categorias cadastradas
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var llm = new FakeLlm();

        var servico = CriarServico(db, llm, Guid.NewGuid());
        var resultado = await servico.CategorizarAsync(["Aluguel", "Supermercado"], CancellationToken.None);

        resultado.Should().HaveCount(2);
        resultado.All(r => r.ContaGerencialId is null).Should().BeTrue();
    }

    [Fact]
    public async Task CategorizarAsync_LlmRetornaJsonValido_RetornaCategoriasCorrespondentes()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var familiaId = _factory.Services.GetRequiredService<IOptions<AuthOptions>>().Value.DevelopmentFamiliaId;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        db.DefinirWorkspaceCorrente(familiaId);
        var llm = new FakeLlm();

        // Resposta com a categoria existente
        var validJson = $$"""
            {
              "categorizacoes": [
                {
                  "descricao": "Despesa Operacional",
                  "contaGerencialId": "{{fixture.ContaGerencialDespesaId}}",
                  "contaGerencialDescricao": "Despesa Operacional",
                  "confianca": 0.95
                }
              ]
            }
            """;

        llm.EnqueueText(validJson);

        var servico = CriarServico(db, llm, familiaId);
        var resultado = await servico.CategorizarAsync(["Aluguel escritório"], CancellationToken.None);

        resultado.Should().ContainSingle();
        resultado[0].ContaGerencialId.Should().Be(fixture.ContaGerencialDespesaId);
        resultado[0].Confianca.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CategorizarAsync_LlmRetornaJsonInvalido_RetornaNullParaCategoria()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        var familiaId = _factory.Services.GetRequiredService<IOptions<AuthOptions>>().Value.DevelopmentFamiliaId;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var llm = new FakeLlm();
        llm.EnqueueText("isso nao e json valido {{{");

        var servico = CriarServico(db, llm, familiaId);
        var resultado = await servico.CategorizarAsync(["Transação X"], CancellationToken.None);

        resultado.Should().ContainSingle();
        resultado[0].ContaGerencialId.Should().BeNull();
    }

    [Fact]
    public async Task CategorizarAsync_LlmRetornaNull_RetornaNullParaCategoria()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        var familiaId = _factory.Services.GetRequiredService<IOptions<AuthOptions>>().Value.DevelopmentFamiliaId;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var llm = new FakeLlm();
        llm.EnqueueText(null!);

        var servico = CriarServico(db, llm, familiaId);
        var resultado = await servico.CategorizarAsync(["Transação Y"], CancellationToken.None);

        resultado.Should().ContainSingle();
        resultado[0].ContaGerencialId.Should().BeNull();
    }

    [Fact]
    public async Task CategorizarAsync_LlmLancaExcecao_RetornaNullSemPropagar()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        var familiaId = _factory.Services.GetRequiredService<IOptions<AuthOptions>>().Value.DevelopmentFamiliaId;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var llm = new FakeLlm();
        llm.EnqueueThrow();

        var servico = CriarServico(db, llm, familiaId);

        var act = async () => await servico.CategorizarAsync(["Transação falha"], CancellationToken.None);
        var resultado = await act.Should().NotThrowAsync();
        resultado.Subject.All(r => r.ContaGerencialId is null).Should().BeTrue();
    }

    [Fact]
    public async Task CategorizarAsync_LlmRetornaGuidInvalido_RetornaNullParaCategoria()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        var familiaId = _factory.Services.GetRequiredService<IOptions<AuthOptions>>().Value.DevelopmentFamiliaId;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var llm = new FakeLlm();

        // Guid não existe no banco — deve ser ignorado
        var jsonGuidFalso = $$"""
            {
              "categorizacoes": [
                {
                  "descricao": "Teste",
                  "contaGerencialId": "00000000-0000-0000-0000-000000000099",
                  "contaGerencialDescricao": "Falsa",
                  "confianca": 0.9
                }
              ]
            }
            """;
        llm.EnqueueText(jsonGuidFalso);

        var servico = CriarServico(db, llm, familiaId);
        var resultado = await servico.CategorizarAsync(["Teste categoria falsa"], CancellationToken.None);

        resultado.Should().ContainSingle();
        resultado[0].ContaGerencialId.Should().BeNull();
        resultado[0].Confianca.Should().Be(0);
    }

    [Fact]
    public async Task CategorizarAsync_FamiliaNaoIdentificada_LancaInvalidOperationException()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var llm = new FakeLlm();

        var servico = new FinanceCategorizacaoService(
            llm, db,
            new FakeCurrentUser(Guid.NewGuid().ToString(), null),
            NullLogger<FinanceCategorizacaoService>.Instance);

        var act = async () => await servico.CategorizarAsync(["Teste"], CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Família*");
    }

    [Fact]
    public async Task CategorizarAsync_LlmRetornaJsonComMarkdown_ParseCorreto()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var familiaId = _factory.Services.GetRequiredService<IOptions<AuthOptions>>().Value.DevelopmentFamiliaId;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        db.DefinirWorkspaceCorrente(familiaId);
        var llm = new FakeLlm();

        // LLM às vezes envolve o JSON em blocos de código markdown
        var jsonComMarkdown = $$"""
            ```json
            {
              "categorizacoes": [
                {
                  "descricao": "Despesa Operacional",
                  "contaGerencialId": "{{fixture.ContaGerencialDespesaId}}",
                  "contaGerencialDescricao": "Despesa Operacional",
                  "confianca": 0.88
                }
              ]
            }
            ```
            """;
        llm.EnqueueText(jsonComMarkdown);

        var servico = CriarServico(db, llm, familiaId);
        var resultado = await servico.CategorizarAsync(["Conta de energia"], CancellationToken.None);

        resultado.Should().ContainSingle();
        resultado[0].ContaGerencialId.Should().Be(fixture.ContaGerencialDespesaId);
    }
}
