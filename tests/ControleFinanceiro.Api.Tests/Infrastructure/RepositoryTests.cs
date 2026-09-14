using System.Net.Http.Json;
using ControleFinanceiro.Api.Tests.Financeiro;
using ControleFinanceiro.Application.Cadastros.ContasGerenciais;
using ControleFinanceiro.Application.Cadastros.Pessoas;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Financeiro.ContasPagar;
using ControleFinanceiro.Application.Financeiro.ContasReceber;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Financeiro;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFinanceiro.Api.Tests.Infrastructure;

public sealed class RepositoryTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private sealed record IdResponse(Guid Id);

    // ─── ContaGerencialRepository ────────────────────────────────────────────

    [Fact]
    public async Task ContaGerencialRepository_ListAtivasAsync_RetornaApenasSAtivas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaGerencialRepository>();

        var result = await repo.ListAtivasAsync();

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(c => c.Ativo);
    }

    [Fact]
    public async Task ContaGerencialRepository_ListByTipoAsync_FiltrapPorTipo()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaGerencialRepository>();

        var despesas = await repo.ListByTipoAsync(TipoContaGerencial.Despesa);
        var receitas = await repo.ListByTipoAsync(TipoContaGerencial.Receita);

        despesas.Should().NotBeEmpty();
        despesas.Should().OnlyContain(c => c.Tipo == TipoContaGerencial.Despesa);
        receitas.Should().NotBeEmpty();
        receitas.Should().OnlyContain(c => c.Tipo == TipoContaGerencial.Receita);
    }

    [Fact]
    public async Task ContaGerencialRepository_ListByContaPaiAsync_RetornaContasRaiz()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaGerencialRepository>();

        var result = await repo.ListByContaPaiAsync(null);

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(c => c.ContaPaiId == null);
    }

    [Fact]
    public async Task ContaGerencialRepository_GetByCodigoAsync_EncontraContaExistente()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaGerencialRepository>();

        var found = await repo.GetByCodigoAsync("DESP");
        var notFound = await repo.GetByCodigoAsync("INEXISTENTE");

        found.Should().NotBeNull();
        found!.Codigo.Should().Be("DESP");
        notFound.Should().BeNull();
    }

    // ─── GenericRepository (via ContaGerencialRepository) ───────────────────

    [Fact]
    public async Task GenericRepository_GetByIdAsync_RetornaEntidadeCorreta()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaGerencialRepository>();

        var found = await repo.GetByIdAsync(fixture.ContaGerencialDespesaId);
        var notFound = await repo.GetByIdAsync(Guid.NewGuid());

        found.Should().NotBeNull();
        found!.Id.Should().Be(fixture.ContaGerencialDespesaId);
        notFound.Should().BeNull();
    }

    [Fact]
    public async Task GenericRepository_ListAsync_RetornaTodosOsRegistros()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaGerencialRepository>();

        var result = await repo.ListAsync();

        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenericRepository_AddAsync_PersisteDados()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaGerencialRepository>();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var conta = ContaGerencial.Criar("TST", "Conta Teste Add", TipoContaGerencial.Despesa,
            null, null, true, false);
        await repo.AddAsync(conta);
        await db.SaveChangesAsync(CancellationToken.None);

        var found = await repo.GetByIdAsync(conta.Id);
        found.Should().NotBeNull();
        found!.Descricao.Should().Be("Conta Teste Add");
    }

    [Fact]
    public async Task GenericRepository_UpdateAsync_AtualizaDados()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaGerencialRepository>();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var conta = await repo.GetByIdAsync(fixture.ContaGerencialDespesaId);
        conta!.Atualizar("DESP", "Despesa Atualizada", TipoContaGerencial.Despesa,
            null, null, true, false);

        await repo.UpdateAsync(conta);
        await db.SaveChangesAsync(CancellationToken.None);

        using var scope2 = _factory.Services.CreateWorkspaceScope();
        var repo2 = scope2.ServiceProvider.GetRequiredService<IContaGerencialRepository>();
        var updated = await repo2.GetByIdAsync(fixture.ContaGerencialDespesaId);
        updated!.Descricao.Should().Be("Despesa Atualizada");
    }

    [Fact]
    public async Task GenericRepository_DeleteAsync_RemoveDados()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaGerencialRepository>();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        // Adiciona uma conta nova para deletar (sem dependências)
        var conta = ContaGerencial.Criar("DEL", "Conta Para Deletar", TipoContaGerencial.Despesa,
            null, null, true, false);
        await repo.AddAsync(conta);
        await db.SaveChangesAsync(CancellationToken.None);

        var id = conta.Id;
        await repo.DeleteAsync(conta);
        await db.SaveChangesAsync(CancellationToken.None);

        using var scope2 = _factory.Services.CreateWorkspaceScope();
        var repo2 = scope2.ServiceProvider.GetRequiredService<IContaGerencialRepository>();
        var deleted = await repo2.GetByIdAsync(id);
        deleted.Should().BeNull();
    }

    // ─── ContaPagarRepository ────────────────────────────────────────────────

    [Fact]
    public async Task ContaPagarRepository_ListByStatusAsync_RetornaContasPendentes()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var resp = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            recebedorId = fixture.RecebedorId,
            dataVencimento = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10).ToString("yyyy-MM-dd"),
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 100m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta Repo Status",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 100m } }
        });
        resp.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaPagarRepository>();

        var result = await repo.ListByStatusAsync(StatusConta.PendenteId);

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(c => c.StatusContaId == StatusConta.PendenteId);
    }

    [Fact]
    public async Task ContaPagarRepository_ListByRecebedorAsync_FiltrapPorRecebedor()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var resp = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            recebedorId = fixture.RecebedorId,
            dataVencimento = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5).ToString("yyyy-MM-dd"),
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 150m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta Repo Recebedor",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 150m } }
        });
        resp.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaPagarRepository>();

        var result = await repo.ListByRecebedorAsync(fixture.RecebedorId);
        var vazio = await repo.ListByRecebedorAsync(Guid.NewGuid());

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(c => c.RecebedorId == fixture.RecebedorId);
        vazio.Should().BeEmpty();
    }

    [Fact]
    public async Task ContaPagarRepository_ListByDataVencimentoAsync_FiltrapPorPeriodo()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var fixture = await FinancialFixtureSeed.CreateAsync(client);

        var vencimento = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);
        var resp = await client.PostAsJsonAsync("/api/v1/contas-pagar", new
        {
            dataEmissao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            recebedorId = fixture.RecebedorId,
            dataVencimento = vencimento.ToString("yyyy-MM-dd"),
            formaPagamentoId = fixture.FormaPagamentoManualId,
            valorOriginal = 200m,
            valorDesconto = 0m,
            valorJuros = 0m,
            valorMulta = 0m,
            quantidadeParcelas = 1,
            descricao = "Conta Repo Data",
            rateios = new[] { new { contaGerencialId = fixture.ContaGerencialDespesaId, valor = 200m } }
        });
        resp.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaPagarRepository>();

        var result = await repo.ListByDataVencimentoAsync(vencimento.AddDays(-1), vencimento.AddDays(1));
        var vazio = await repo.ListByDataVencimentoAsync(vencimento.AddDays(30), vencimento.AddDays(60));

        result.Should().NotBeEmpty();
        vazio.Should().BeEmpty();
    }

    [Fact]
    public async Task ContaPagarRepository_ListByGrupoParcelamentoAsync_RetornaVazioParaGrupoInexistente()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaPagarRepository>();

        var result = await repo.ListByGrupoParcelamentoAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    // ─── PessoaRepository ────────────────────────────────────────────────────

    [Fact]
    public async Task PessoaRepository_ListAtivasAsync_RetornaApenasPessoasAtivas()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPessoaRepository>();

        var result = await repo.ListAtivasAsync();

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(p => p.Ativo);
    }

    [Fact]
    public async Task PessoaRepository_ListByTipoAsync_FiltrapPorTipo()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await FinancialFixtureSeed.CreateAsync(client);

        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPessoaRepository>();

        var fisica = await repo.ListByTipoAsync(TipoPessoa.Fisica);
        var juridica = await repo.ListByTipoAsync(TipoPessoa.Juridica);

        fisica.Should().NotBeEmpty();
        juridica.Should().NotBeEmpty();
        fisica.Should().OnlyContain(p => p.TipoPessoa == TipoPessoa.Fisica);
        juridica.Should().OnlyContain(p => p.TipoPessoa == TipoPessoa.Juridica);
    }

    [Fact]
    public async Task PessoaRepository_GetByIdWithChavesPixAsync_RetornaNullParaIdInexistente()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPessoaRepository>();

        var result = await repo.GetByIdWithChavesPixAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task PessoaRepository_GetByCpfCnpjAsync_RetornaNullParaCpfInexistente()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPessoaRepository>();

        var result = await repo.GetByCpfCnpjAsync("000.000.000-00");

        result.Should().BeNull();
    }

    // ─── ContaReceberRepository ──────────────────────────────────────────────

    [Fact]
    public async Task ContaReceberRepository_ListByStatusAsync_RetornaVazioSemDados()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaReceberRepository>();

        var result = await repo.ListByStatusAsync(StatusConta.PendenteId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ContaReceberRepository_ListByGrupoParcelamentoAsync_RetornaVazioParaGrupoInexistente()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaReceberRepository>();

        var result = await repo.ListByGrupoParcelamentoAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ContaReceberRepository_ListByDataVencimentoAsync_RetornaVazioSemDados()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateWorkspaceScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaReceberRepository>();

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await repo.ListByDataVencimentoAsync(hoje, hoje.AddDays(30));

        result.Should().BeEmpty();
    }

}
