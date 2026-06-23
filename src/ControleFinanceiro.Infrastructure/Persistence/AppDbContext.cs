using System.Text.Json;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Cadastros.FormasPagamento;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Anexos;
using ControleFinanceiro.Domain.FinanceAI;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.Identidade;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using ControleFinanceiro.Domain.PlanejamentoCompras;
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
    private Guid? _familiaCorrente = currentUser?.FamiliaId;

    /// <summary>
    /// Tenant efetivo da instância. Quando nulo (workers, webhook anônimo, testes de infra),
    /// os filtros de consulta por família ficam desativados e nada é estampado na inserção.
    /// </summary>
    public Guid? FamiliaCorrente => _familiaCorrente;

    public void DefinirFamiliaCorrente(Guid familiaId)
    {
        if (familiaId == Guid.Empty)
        {
            throw new ArgumentException("Família é obrigatória.", nameof(familiaId));
        }

        _familiaCorrente = familiaId;
    }

    public DbSet<AuditTrailEntry> AuditTrailEntries => Set<AuditTrailEntry>();

    public DbSet<Anexo> Anexos => Set<Anexo>();

    public DbSet<AnexoVinculo> AnexoVinculos => Set<AnexoVinculo>();

    public DbSet<Pessoa> Pessoas => Set<Pessoa>();

    public DbSet<PessoaChavePix> PessoasChavesPix => Set<PessoaChavePix>();

    public DbSet<FormaPagamento> FormasPagamento => Set<FormaPagamento>();

    public DbSet<ContaBancaria> ContasBancarias => Set<ContaBancaria>();

    public DbSet<Cartao> Cartoes => Set<Cartao>();

    public DbSet<ContaGerencial> ContasGerenciais => Set<ContaGerencial>();

    public DbSet<StatusConta> StatusContas => Set<StatusConta>();

    public DbSet<StatusMovimentacao> StatusMovimentacoes => Set<StatusMovimentacao>();

    public DbSet<ContaPagar> ContasPagar => Set<ContaPagar>();

    public DbSet<ContaReceber> ContasReceber => Set<ContaReceber>();

    public DbSet<RateioContaGerencial> RateiosContaGerencial => Set<RateioContaGerencial>();

    public DbSet<MetaOrcamento> MetasOrcamento => Set<MetaOrcamento>();

    public DbSet<MovimentacaoFinanceira> MovimentacoesFinanceiras => Set<MovimentacaoFinanceira>();

    public DbSet<FaturaCartao> FaturasCartao => Set<FaturaCartao>();

    public DbSet<RegraRecorrencia> RegrasRecorrencia => Set<RegraRecorrencia>();

    public DbSet<ImportacaoWhatsapp> ImportacoesWhatsapp => Set<ImportacaoWhatsapp>();

    public DbSet<ItemImportadoWhatsapp> ItensImportadosWhatsapp => Set<ItemImportadoWhatsapp>();

    public DbSet<PlanejamentoCompra> ComprasPlanejadas => Set<PlanejamentoCompra>();

    public DbSet<AiConversa> AiConversas => Set<AiConversa>();

    public DbSet<AiMensagem> AiMensagens => Set<AiMensagem>();

    public DbSet<AiToolCall> AiToolCalls => Set<AiToolCall>();

    public DbSet<WhatsappUsuario> WhatsappUsuarios => Set<WhatsappUsuario>();

    public DbSet<WhatsappConfigAlerta> WhatsappConfigAlertas => Set<WhatsappConfigAlerta>();

    public DbSet<AlertaWhatsappEnviado> AlertasWhatsappEnviados => Set<AlertaWhatsappEnviado>();

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Familia> Familias => Set<Familia>();

    public DbSet<MembroFamilia> MembrosFamilia => Set<MembroFamilia>();

    public DbSet<ConviteFamilia> ConvitesFamilia => Set<ConviteFamilia>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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
        modelBuilder.ApplyConfiguration(new AnexoConfiguration());
        modelBuilder.ApplyConfiguration(new AnexoVinculoConfiguration());
        modelBuilder.ApplyConfiguration(new PessoaConfiguration());
        modelBuilder.ApplyConfiguration(new PessoaChavePixConfiguration());
        modelBuilder.ApplyConfiguration(new FormaPagamentoConfiguration());
        modelBuilder.ApplyConfiguration(new ContaBancariaConfiguration());
        modelBuilder.ApplyConfiguration(new CartaoConfiguration());
        modelBuilder.ApplyConfiguration(new ContaGerencialConfiguration());
        modelBuilder.ApplyConfiguration(new StatusContaConfiguration());
        modelBuilder.ApplyConfiguration(new StatusMovimentacaoConfiguration());
        modelBuilder.ApplyConfiguration(new ContaPagarConfiguration());
        modelBuilder.ApplyConfiguration(new ContaReceberConfiguration());
        modelBuilder.ApplyConfiguration(new RateioContaGerencialConfiguration());
        modelBuilder.ApplyConfiguration(new MetaOrcamentoConfiguration());
        modelBuilder.ApplyConfiguration(new MovimentacaoFinanceiraConfiguration());
        modelBuilder.ApplyConfiguration(new FaturaCartaoConfiguration());
        modelBuilder.ApplyConfiguration(new RegraRecorrenciaConfiguration());
        modelBuilder.ApplyConfiguration(new ImportacaoWhatsappConfiguration());
        modelBuilder.ApplyConfiguration(new ItemImportadoWhatsappConfiguration());
        modelBuilder.ApplyConfiguration(new PlanejamentoCompraConfiguration());
        modelBuilder.ApplyConfiguration(new UsuarioConfiguration());
        modelBuilder.ApplyConfiguration(new FamiliaConfiguration());
        modelBuilder.ApplyConfiguration(new MembroFamiliaConfiguration());
        modelBuilder.ApplyConfiguration(new ConviteFamiliaConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new AiConversaConfiguration());
        modelBuilder.ApplyConfiguration(new AiMensagemConfiguration());
        modelBuilder.ApplyConfiguration(new AiToolCallConfiguration());
        modelBuilder.ApplyConfiguration(new WhatsappUsuarioConfiguration());
        modelBuilder.ApplyConfiguration(new WhatsappConfigAlertaConfiguration());
        AplicarConvencoesDeTenant(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private void AplicarConvencoesDeTenant(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var aplicarFiltro = typeof(AppDbContext)
                .GetMethod(nameof(AplicarFiltroDeTenant), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);

            aplicarFiltro.Invoke(this, [modelBuilder]);

            modelBuilder.Entity(entityType.ClrType)
                .HasIndex(nameof(ITenantEntity.FamiliaId));
        }
    }

    private void AplicarFiltroDeTenant<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(entity => _familiaCorrente == null || (Guid?)entity.FamiliaId == _familiaCorrente);
    }

    private void PrepareAuditableEntities()
    {
        var utcNow = _clock.UtcNow;
        var userId = _currentUser.UserId;

        var auditableEntries = ChangeTracker
            .Entries<AuditableEntity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .ToList();

        if (auditableEntries.Count == 0) return;

        foreach (var entry in auditableEntries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.StampCreation(utcNow, userId);

                if (entry.Entity is ITenantEntity tenantEntity
                    && tenantEntity.FamiliaId == Guid.Empty
                    && _familiaCorrente.HasValue)
                {
                    tenantEntity.AtribuirFamilia(_familiaCorrente.Value);
                }
            }

            if (entry.State == EntityState.Added)
            {
                AuditTrailEntries.Add(CriarAuditTrail(entry, utcNow, userId));
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.StampUpdate(utcNow, userId);
                AuditTrailEntries.Add(CriarAuditTrail(entry, utcNow, userId));
            }
        }
    }

    private static AuditTrailEntry CriarAuditTrail(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<AuditableEntity> entry,
        DateTime occurredAtUtc,
        string? userId)
    {
        var entityName = entry.Entity.GetType().Name;
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
            entityName,
            entry.Entity.Id,
            action,
            occurredAtUtc,
            userId,
            AuditTrailSerializer.Sanitize(entityName, beforeJson),
            AuditTrailSerializer.Sanitize(entityName, afterJson));
    }

    private sealed class DefaultClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private sealed class AnonymousCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => false;

        public string? UserId => null;

        public Guid? FamiliaId => null;

        public string? Papel => null;
    }
}
