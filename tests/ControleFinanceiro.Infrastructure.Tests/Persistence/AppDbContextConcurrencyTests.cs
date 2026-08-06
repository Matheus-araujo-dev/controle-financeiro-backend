using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Infrastructure.Persistence;
using ControleFinanceiro.SharedKernel.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Infrastructure.Tests.Persistence;

public sealed class AppDbContextConcurrencyTests
{
    [Fact]
    public async Task LiquidacoesConcorrentes_NaMesmaConta_DevemFalharNaSegunda()
    {
        // FKs desabilitadas: o teste isola o mecanismo de concorrência, não a integridade referencial.
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var clock = new FakeClock(new DateTime(2026, 4, 3, 20, 30, 0, DateTimeKind.Utc));
        var currentUser = new FakeCurrentUser("tester");

        Guid contaId;
        await using (var seed = new AppDbContext(options, clock, currentUser))
        {
            await seed.Database.EnsureCreatedAsync();
            var conta = CriarContaPendente();
            seed.ContasPagar.Add(conta);
            await seed.SaveChangesAsync();
            contaId = conta.Id;
        }

        // Dois contextos independentes carregam a MESMA conta (mesmo ConcurrencyStamp original).
        await using var contextoA = new AppDbContext(options, clock, currentUser);
        await using var contextoB = new AppDbContext(options, clock, currentUser);

        var contaA = await contextoA.ContasPagar.SingleAsync(x => x.Id == contaId);
        var contaB = await contextoB.ContasPagar.SingleAsync(x => x.Id == contaId);

        contaA.Liquidar(new DateOnly(2026, 4, 15), Guid.NewGuid(), StatusConta.LiquidadaId);
        contaB.Liquidar(new DateOnly(2026, 4, 15), Guid.NewGuid(), StatusConta.LiquidadaId);

        // A primeira gravação vence e renova o ConcurrencyStamp.
        await contextoA.SaveChangesAsync();

        // A segunda gravação usa o stamp original (já obsoleto) no WHERE → 0 linhas → conflito.
        var act = async () => await contextoB.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private static ContaPagar CriarContaPendente()
    {
        return ContaPagar.Criar(
            numeroDocumento: "DOC-001",
            dataEmissao: new DateOnly(2026, 4, 1),
            responsavelCompraId: null,
            recebedorId: Guid.NewGuid(),
            dataVencimento: new DateOnly(2026, 4, 10),
            formaPagamentoId: Guid.NewGuid(),
            cartaoId: null,
            contaBancariaId: null,
            valorOriginal: 100m,
            valorDesconto: 0m,
            valorJuros: 0m,
            valorMulta: 0m,
            quantidadeParcelas: 1,
            numeroParcela: 1,
            grupoParcelamentoId: null,
            origemCompraPlanejadaId: null,
            descricao: "Conta teste",
            observacao: null,
            statusContaId: StatusConta.PendenteId,
            ehRecorrente: false,
            regraRecorrenciaId: null,
            origem: OrigemLancamento.Manual,
            rateios: [RateioPlano.Create(Guid.NewGuid(), 100m)]);
    }

    private sealed class FakeClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FakeCurrentUser(string userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public string? UserId { get; } = userId;
        public string? UserEmail => null;
        public Guid? WorkspaceId => null;
        public Guid? FamiliaId => WorkspaceId;
        public string? Papel => null;
    }
}
