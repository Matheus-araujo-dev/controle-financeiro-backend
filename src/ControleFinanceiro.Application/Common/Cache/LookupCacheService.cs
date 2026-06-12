using System.Collections.Concurrent;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Cadastros.FormasPagamento;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControleFinanceiro.Application.Common.Cache;

public sealed class LookupCacheService : ILookupCacheService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LookupCacheService> _logger;
    private readonly TimeSpan _defaultExpiration;
    private readonly ConcurrentDictionary<string, object> _cache = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public LookupCacheService(IServiceScopeFactory scopeFactory)
        : this(scopeFactory, Microsoft.Extensions.Logging.Abstractions.NullLogger<LookupCacheService>.Instance, TimeSpan.FromMinutes(30))
    {
    }

    public LookupCacheService(
        IServiceScopeFactory scopeFactory,
        ILogger<LookupCacheService> logger,
        TimeSpan? defaultExpiration = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _defaultExpiration = defaultExpiration ?? TimeSpan.FromMinutes(30);
    }

    public async Task<IReadOnlyList<StatusConta>> GetAllStatusContaAsync(CancellationToken cancellationToken)
    {
        var entry = GetOrCreateStatusContaEntry();
        return await entry.GetAsync(cancellationToken);
    }

    public async Task<StatusConta?> GetStatusContaByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var items = await GetAllStatusContaAsync(cancellationToken);
        return items.FirstOrDefault(x => x.Id == id);
    }

    public async Task<IReadOnlyList<StatusMovimentacao>> GetAllStatusMovimentacaoAsync(CancellationToken cancellationToken)
    {
        var entry = GetOrCreateStatusMovimentacaoEntry();
        return await entry.GetAsync(cancellationToken);
    }

    public async Task<StatusMovimentacao?> GetStatusMovimentacaoByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var items = await GetAllStatusMovimentacaoAsync(cancellationToken);
        return items.FirstOrDefault(x => x.Id == id);
    }

    public async Task<IReadOnlyList<FormaPagamento>> GetAllFormaPagamentoAsync(CancellationToken cancellationToken)
    {
        var entry = GetOrCreateFormaPagamentoEntry();
        return await entry.GetAsync(cancellationToken);
    }

    public async Task<FormaPagamento?> GetFormaPagamentoByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var items = await GetAllFormaPagamentoAsync(cancellationToken);
        return items.FirstOrDefault(x => x.Id == id);
    }

    public async Task<IReadOnlyList<ContaGerencial>> GetAllContaGerencialAsync(CancellationToken cancellationToken)
    {
        var entry = GetOrCreateContaGerencialEntry();
        return await entry.GetAsync(cancellationToken);
    }

    public async Task<ContaGerencial?> GetContaGerencialByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var items = await GetAllContaGerencialAsync(cancellationToken);
        return items.FirstOrDefault(x => x.Id == id);
    }

    public async ValueTask RefreshStatusContaAsync(CancellationToken cancellationToken)
    {
        await InvalidateEntryAsync("StatusConta", cancellationToken);
    }

    public async ValueTask RefreshStatusMovimentacaoAsync(CancellationToken cancellationToken)
    {
        await InvalidateEntryAsync("StatusMovimentacao", cancellationToken);
    }

    public async ValueTask RefreshFormaPagamentoAsync(CancellationToken cancellationToken)
    {
        await InvalidateEntryAsync("FormaPagamento", cancellationToken);
    }

    public async ValueTask RefreshContaGerencialAsync(CancellationToken cancellationToken)
    {
        await InvalidateEntryAsync("ContaGerencial", cancellationToken);
    }

    public async ValueTask RefreshAllAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Refreshing all lookup caches");
            if (_cache.TryGetValue("StatusConta", out var sc)) ((IInvalidate)sc).Invalidate();
            if (_cache.TryGetValue("StatusMovimentacao", out var sm)) ((IInvalidate)sm).Invalidate();
            if (_cache.TryGetValue("FormaPagamento", out var fp)) ((IInvalidate)fp).Invalidate();
            if (_cache.TryGetValue("ContaGerencial", out var cg)) ((IInvalidate)cg).Invalidate();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private StatusContaCacheEntry GetOrCreateStatusContaEntry()
    {
        const string key = "StatusConta";
        if (_cache.TryGetValue(key, out var existingEntry))
        {
            return (StatusContaCacheEntry)existingEntry;
        }

        var newEntry = new StatusContaCacheEntry(_scopeFactory, _defaultExpiration);
        _cache[key] = newEntry;
        return newEntry;
    }

    private StatusMovimentacaoCacheEntry GetOrCreateStatusMovimentacaoEntry()
    {
        const string key = "StatusMovimentacao";
        if (_cache.TryGetValue(key, out var existingEntry))
        {
            return (StatusMovimentacaoCacheEntry)existingEntry;
        }

        var newEntry = new StatusMovimentacaoCacheEntry(_scopeFactory, _defaultExpiration);
        _cache[key] = newEntry;
        return newEntry;
    }

    private FormaPagamentoCacheEntry GetOrCreateFormaPagamentoEntry()
    {
        const string key = "FormaPagamento";
        if (_cache.TryGetValue(key, out var existingEntry))
        {
            return (FormaPagamentoCacheEntry)existingEntry;
        }

        var newEntry = new FormaPagamentoCacheEntry(_scopeFactory, _defaultExpiration);
        _cache[key] = newEntry;
        return newEntry;
    }

    private ContaGerencialCacheEntry GetOrCreateContaGerencialEntry()
    {
        const string key = "ContaGerencial";
        if (_cache.TryGetValue(key, out var existingEntry))
        {
            return (ContaGerencialCacheEntry)existingEntry;
        }

        var newEntry = new ContaGerencialCacheEntry(_scopeFactory, _defaultExpiration);
        _cache[key] = newEntry;
        return newEntry;
    }

    private async ValueTask InvalidateEntryAsync(string key, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Refreshing cache for {EntityType}", key);
                ((IInvalidate)entry).Invalidate();
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }

    private interface IInvalidate
    {
        void Invalidate();
    }

    private sealed class StatusContaCacheEntry : IInvalidate
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _expiration;
        private IReadOnlyList<StatusConta>? _cachedItems;
        private DateTime _lastRefresh;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public StatusContaCacheEntry(IServiceScopeFactory scopeFactory, TimeSpan expiration)
        {
            _scopeFactory = scopeFactory;
            _expiration = expiration;
            _lastRefresh = DateTime.MinValue;
        }

        public async Task<IReadOnlyList<StatusConta>> GetAsync(CancellationToken cancellationToken)
        {
            if (_cachedItems is not null && !IsExpired())
            {
                return _cachedItems;
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_cachedItems is not null && !IsExpired())
                {
                    return _cachedItems;
                }

                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IStatusDbContext>();
                _cachedItems = await dbContext.StatusContas.AsNoTracking().ToListAsync(cancellationToken);
                _lastRefresh = DateTime.UtcNow;
                return _cachedItems;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Invalidate()
        {
            _cachedItems = null;
            _lastRefresh = DateTime.MinValue;
        }

        private bool IsExpired() => DateTime.UtcNow - _lastRefresh > _expiration;
    }

    private sealed class StatusMovimentacaoCacheEntry : IInvalidate
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _expiration;
        private IReadOnlyList<StatusMovimentacao>? _cachedItems;
        private DateTime _lastRefresh;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public StatusMovimentacaoCacheEntry(IServiceScopeFactory scopeFactory, TimeSpan expiration)
        {
            _scopeFactory = scopeFactory;
            _expiration = expiration;
            _lastRefresh = DateTime.MinValue;
        }

        public async Task<IReadOnlyList<StatusMovimentacao>> GetAsync(CancellationToken cancellationToken)
        {
            if (_cachedItems is not null && !IsExpired())
            {
                return _cachedItems;
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_cachedItems is not null && !IsExpired())
                {
                    return _cachedItems;
                }

                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IStatusDbContext>();
                _cachedItems = await dbContext.StatusMovimentacoes.AsNoTracking().ToListAsync(cancellationToken);
                _lastRefresh = DateTime.UtcNow;
                return _cachedItems;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Invalidate()
        {
            _cachedItems = null;
            _lastRefresh = DateTime.MinValue;
        }

        private bool IsExpired() => DateTime.UtcNow - _lastRefresh > _expiration;
    }

    private sealed class FormaPagamentoCacheEntry : IInvalidate
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _expiration;
        private IReadOnlyList<FormaPagamento>? _cachedItems;
        private DateTime _lastRefresh;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public FormaPagamentoCacheEntry(IServiceScopeFactory scopeFactory, TimeSpan expiration)
        {
            _scopeFactory = scopeFactory;
            _expiration = expiration;
            _lastRefresh = DateTime.MinValue;
        }

        public async Task<IReadOnlyList<FormaPagamento>> GetAsync(CancellationToken cancellationToken)
        {
            if (_cachedItems is not null && !IsExpired())
            {
                return _cachedItems;
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_cachedItems is not null && !IsExpired())
                {
                    return _cachedItems;
                }

                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ICadastrosDbContext>();
                _cachedItems = await dbContext.FormasPagamento.AsNoTracking().ToListAsync(cancellationToken);
                _lastRefresh = DateTime.UtcNow;
                return _cachedItems;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Invalidate()
        {
            _cachedItems = null;
            _lastRefresh = DateTime.MinValue;
        }

        private bool IsExpired() => DateTime.UtcNow - _lastRefresh > _expiration;
    }

    private sealed class ContaGerencialCacheEntry : IInvalidate
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _expiration;
        private IReadOnlyList<ContaGerencial>? _cachedItems;
        private DateTime _lastRefresh;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public ContaGerencialCacheEntry(IServiceScopeFactory scopeFactory, TimeSpan expiration)
        {
            _scopeFactory = scopeFactory;
            _expiration = expiration;
            _lastRefresh = DateTime.MinValue;
        }

        public async Task<IReadOnlyList<ContaGerencial>> GetAsync(CancellationToken cancellationToken)
        {
            if (_cachedItems is not null && !IsExpired())
            {
                return _cachedItems;
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_cachedItems is not null && !IsExpired())
                {
                    return _cachedItems;
                }

                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ICadastrosDbContext>();
                _cachedItems = await dbContext.ContasGerenciais.AsNoTracking().ToListAsync(cancellationToken);
                _lastRefresh = DateTime.UtcNow;
                return _cachedItems;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Invalidate()
        {
            _cachedItems = null;
            _lastRefresh = DateTime.MinValue;
        }

        private bool IsExpired() => DateTime.UtcNow - _lastRefresh > _expiration;
    }
}