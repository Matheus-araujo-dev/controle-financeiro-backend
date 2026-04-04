using System.Text.Json;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Cadastros.FormasPagamento;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.SharedKernel.Abstractions;
using ControleFinanceiro.SharedKernel.Common;
using ControleFinanceiro.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IClock? clock = null,
    ICurrentUser? currentUser = null) : DbContext(options), IAppDbContext
{
    private readonly IClock _clock = clock ?? new DefaultClock();
    private readonly ICurrentUser _currentUser = currentUser ?? new AnonymousCurrentUser();

    public DbSet<AuditTrailEntry> AuditTrailEntries => Set<AuditTrailEntry>();

    public DbSet<Pessoa> Pessoas => Set<Pessoa>();

    public DbSet<FormaPagamento> FormasPagamento => Set<FormaPagamento>();

    public DbSet<ContaBancaria> ContasBancarias => Set<ContaBancaria>();

    public DbSet<Cartao> Cartoes => Set<Cartao>();

    public DbSet<ContaGerencial> ContasGerenciais => Set<ContaGerencial>();

    public DbSet<StatusConta> StatusContas => Set<StatusConta>();

    public DbSet<StatusMovimentacao> StatusMovimentacoes => Set<StatusMovimentacao>();

    public DbSet<ContaPagar> ContasPagar => Set<ContaPagar>();

    public DbSet<ContaReceber> ContasReceber => Set<ContaReceber>();

    public DbSet<RateioContaGerencial> RateiosContaGerencial => Set<RateioContaGerencial>();

    public DbSet<MovimentacaoFinanceira> MovimentacoesFinanceiras => Set<MovimentacaoFinanceira>();

    public override int SaveChanges()
    {
        PrepareAuditableEntities();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PrepareAuditableEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AuditTrailEntryConfiguration());
        modelBuilder.ApplyConfiguration(new PessoaConfiguration());
        modelBuilder.ApplyConfiguration(new FormaPagamentoConfiguration());
        modelBuilder.ApplyConfiguration(new ContaBancariaConfiguration());
        modelBuilder.ApplyConfiguration(new CartaoConfiguration());
        modelBuilder.ApplyConfiguration(new ContaGerencialConfiguration());
        modelBuilder.ApplyConfiguration(new StatusContaConfiguration());
        modelBuilder.ApplyConfiguration(new StatusMovimentacaoConfiguration());
        modelBuilder.ApplyConfiguration(new ContaPagarConfiguration());
        modelBuilder.ApplyConfiguration(new ContaReceberConfiguration());
        modelBuilder.ApplyConfiguration(new RateioContaGerencialConfiguration());
        modelBuilder.ApplyConfiguration(new MovimentacaoFinanceiraConfiguration());
        base.OnModelCreating(modelBuilder);
    }

    private void PrepareAuditableEntities()
    {
        var utcNow = _clock.UtcNow;
        var userId = _currentUser.UserId;

        var auditableEntries = ChangeTracker
            .Entries<AuditableEntity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .ToArray();

        foreach (var entry in auditableEntries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.StampCreation(utcNow, userId);
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.StampUpdate(utcNow, userId);
            }
        }

        foreach (var entry in auditableEntries)
        {
            AuditTrailEntries.Add(CriarAuditTrail(entry, utcNow, userId));
        }
    }

    private static AuditTrailEntry CriarAuditTrail(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<AuditableEntity> entry,
        DateTime occurredAtUtc,
        string? userId)
    {
        var beforeJson = entry.State == EntityState.Modified
            ? JsonSerializer.Serialize(entry.OriginalValues.Properties.ToDictionary(
                property => property.Name,
                property => entry.OriginalValues[property]))
            : null;

        var afterJson = JsonSerializer.Serialize(entry.CurrentValues.Properties.ToDictionary(
            property => property.Name,
            property => entry.CurrentValues[property]));

        var action = entry.State == EntityState.Added ? "Created" : "Updated";

        return AuditTrailEntry.Create(
            entry.Entity.GetType().Name,
            entry.Entity.Id,
            action,
            occurredAtUtc,
            userId,
            beforeJson,
            afterJson);
    }

    private sealed class DefaultClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private sealed class AnonymousCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => false;

        public string? UserId => null;
    }
}
