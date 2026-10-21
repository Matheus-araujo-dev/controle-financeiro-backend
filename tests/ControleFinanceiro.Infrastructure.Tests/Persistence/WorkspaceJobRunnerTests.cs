using ControleFinanceiro.Application.Common;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Identidade;
using ControleFinanceiro.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControleFinanceiro.Infrastructure.Tests.Persistence;

public sealed class WorkspaceJobRunnerTests
{
    [Fact]
    public async Task Job_DeveIsolarCadaWorkspaceEContinuarAposFalha()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        await using var provider = services.BuildServiceProvider();
        var familias = new[] { Familia.Criar("A"), Familia.Criar("B") };
        using (var seed = provider.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Familias.AddRange(familias);
            foreach (var familia in familias)
            {
                var pessoa = Pessoa.Criar(familia.Nome, TipoPessoa.Fisica, null, null, null, null, [], true);
                pessoa.AtribuirFamilia(familia.Id);
                db.Pessoas.Add(pessoa);
            }
            await db.SaveChangesAsync();
        }
        var processados = new List<Guid>();
        var contextos = new List<IAppDbContext>();
        var visiveis = new List<(Guid Workspace, Guid[] Familias)>();
        await WorkspaceJobRunner.RunAsync<IAppDbContext>(provider.GetRequiredService<IServiceScopeFactory>(), async (db, token) =>
        {
            contextos.Add(db);
            processados.Add(db.WorkspaceCorrente!.Value);
            visiveis.Add((db.WorkspaceCorrente.Value, await db.Pessoas.Select(p => p.FamiliaId).ToArrayAsync(token)));
            if (processados.Count == 1) throw new InvalidOperationException("Falha isolada de um workspace");
        }, NullLogger.Instance, CancellationToken.None);
        processados.Should().BeEquivalentTo(familias.Select(f => f.Id));
        contextos[0].Should().NotBeSameAs(contextos[1]);
        foreach (var item in visiveis) item.Familias.Should().Equal(item.Workspace);
        using var verificar = provider.CreateScope();
        var semWorkspace = verificar.ServiceProvider.GetRequiredService<AppDbContext>();
        (await semWorkspace.Pessoas.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Job_Cancelado_NaoDeveExecutarOperacoes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        await using var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var executou = false;
        var executar = () => WorkspaceJobRunner.RunAsync<IAppDbContext>(provider.GetRequiredService<IServiceScopeFactory>(), (_, _) =>
        {
            executou = true;
            return Task.CompletedTask;
        }, NullLogger.Instance, new CancellationToken(true));
        await executar.Should().ThrowAsync<OperationCanceledException>();
        executou.Should().BeFalse();
    }
}
