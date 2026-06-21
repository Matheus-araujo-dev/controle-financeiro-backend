using ControleFinanceiro.SharedKernel.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Infrastructure.Tests.SharedKernel;

public sealed class LogHandlerTests
{
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => new Scope();
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception), exception));

        private sealed class Scope : IDisposable
        {
            public void Dispose() { }
        }
    }

    private static (LogHandler<LogHandlerTests> handler, RecordingLogger<LogHandlerTests> logger) Criar()
    {
        var logger = new RecordingLogger<LogHandlerTests>();
        return (new LogHandler<LogHandlerTests>(logger), logger);
    }

    [Fact]
    public void LogInfo_LogWarning_LogError_DeveEncaminharNoNivelCorreto()
    {
        var (handler, logger) = Criar();

        handler.LogInfo("info {V}", 1);
        handler.LogWarning("warn {V}", 2);
        handler.LogError("erro {V}", 3);

        logger.Entries.Should().HaveCount(3);
        logger.Entries[0].Level.Should().Be(LogLevel.Information);
        logger.Entries[1].Level.Should().Be(LogLevel.Warning);
        logger.Entries[2].Level.Should().Be(LogLevel.Error);
    }

    [Fact]
    public void LogError_ComExcecao_DeveIncluirExcecao()
    {
        var (handler, logger) = Criar();
        var ex = new InvalidOperationException("boom");

        handler.LogError(ex, "falhou {V}", 9);

        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Error);
        logger.Entries[0].Exception.Should().BeSameAs(ex);
    }

    [Fact]
    public void Overloads_ComCorrelationId_DeveEncaminharNoNivelCorreto()
    {
        var (handler, logger) = Criar();
        var ex = new Exception("x");

        handler.LogInfo("corr-1", "info {V}", 1);
        handler.LogWarning("corr-2", "warn {V}", 2);
        handler.LogError("corr-3", "erro {V}", 3);
        handler.LogError("corr-4", ex, "erro ex {V}", 4);

        logger.Entries.Select(e => e.Level).Should().Equal(
            LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Error);
        logger.Entries[3].Exception.Should().BeSameAs(ex);
    }
}
