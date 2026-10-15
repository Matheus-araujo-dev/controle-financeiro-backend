using System.Globalization;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Contracts.Financeiro.Fechamentos;
using ControleFinanceiro.Domain.Conciliacao;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Financeiro.Fechamentos;

public sealed class FechamentoMensalAppService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<FechamentoMensalResponse> ObterAsync(string competencia, CancellationToken cancellationToken)
    {
        var periodo = ObterPeriodo(competencia);
        return await MontarRespostaAsync(competencia, periodo.Inicio, periodo.Fim, cancellationToken);
    }

    public async Task<FechamentoMensalResponse> FecharAsync(string competencia, CancellationToken cancellationToken)
    {
        var usuarioId = ObterUsuarioId();
        var periodo = ObterPeriodo(competencia);
        var diagnostico = await MontarRespostaAsync(competencia, periodo.Inicio, periodo.Fim, cancellationToken);
        if (!diagnostico.ProntoParaFechar)
            throw new InvalidOperationException($"O mês possui {diagnostico.QuantidadeBloqueios} bloqueio(s) e não pode ser fechado.");

        var fechamento = await db.FechamentosMensais.SingleOrDefaultAsync(x => x.Competencia == competencia, cancellationToken);
        var agora = new DateTimeOffset(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc));
        if (fechamento is null)
        {
            fechamento = FechamentoMensal.Fechar(competencia, usuarioId, agora,
                diagnostico.TotalReceitas, diagnostico.TotalDespesas, diagnostico.TotalPendente,
                diagnostico.TotalVencido, diagnostico.QuantidadeLancamentos, diagnostico.QuantidadeBloqueios);
            db.FechamentosMensais.Add(fechamento);
        }
        else if (fechamento.Status == StatusFechamentoMensal.Reaberto)
        {
            fechamento.FecharNovamente(usuarioId, agora,
                diagnostico.TotalReceitas, diagnostico.TotalDespesas, diagnostico.TotalPendente,
                diagnostico.TotalVencido, diagnostico.QuantidadeLancamentos, diagnostico.QuantidadeBloqueios);
        }
        else
        {
            throw new InvalidOperationException("O mês já está fechado.");
        }

        await db.SaveChangesAsync(cancellationToken);
        return await MontarRespostaAsync(competencia, periodo.Inicio, periodo.Fim, cancellationToken);
    }

    public async Task<FechamentoMensalResponse> ReabrirAsync(string competencia, string justificativa, CancellationToken cancellationToken)
    {
        var fechamento = await db.FechamentosMensais.SingleOrDefaultAsync(x => x.Competencia == competencia, cancellationToken)
            ?? throw new KeyNotFoundException("Fechamento mensal não encontrado.");
        fechamento.Reabrir(ObterUsuarioId(), new DateTimeOffset(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc)), justificativa);
        await db.SaveChangesAsync(cancellationToken);
        var periodo = ObterPeriodo(competencia);
        return await MontarRespostaAsync(competencia, periodo.Inicio, periodo.Fim, cancellationToken);
    }

    private async Task<FechamentoMensalResponse> MontarRespostaAsync(string competencia, DateOnly inicio, DateOnly fim, CancellationToken ct)
    {
        var contasPagar = db.ContasPagar.AsNoTracking().Where(x => x.DataVencimento >= inicio && x.DataVencimento <= fim && x.StatusContaId != StatusConta.CanceladaId && x.StatusContaId != StatusConta.FuturoId);
        var contasReceber = db.ContasReceber.AsNoTracking().Where(x => x.DataVencimento >= inicio && x.DataVencimento <= fim && x.StatusContaId != StatusConta.CanceladaId && x.StatusContaId != StatusConta.FuturoId);
        var pendentesPagar = contasPagar.Where(x => x.StatusContaId == StatusConta.PendenteId || x.StatusContaId == StatusConta.ParcialId);
        var pendentesReceber = contasReceber.Where(x => x.StatusContaId == StatusConta.PendenteId || x.StatusContaId == StatusConta.ParcialId);

        var totalDespesas = await contasPagar.SumAsync(x => (decimal?)x.ValorLiquido, ct) ?? 0m;
        var totalReceitas = await contasReceber.SumAsync(x => (decimal?)x.ValorLiquido, ct) ?? 0m;
        var totalPendente = (await pendentesPagar.SumAsync(x => (decimal?)x.ValorLiquido, ct) ?? 0m)
            + (await pendentesReceber.SumAsync(x => (decimal?)x.ValorLiquido, ct) ?? 0m);
        var totalVencido = (await contasPagar.Where(x => x.StatusContaId == StatusConta.VencidaId).SumAsync(x => (decimal?)x.ValorLiquido, ct) ?? 0m)
            + (await contasReceber.Where(x => x.StatusContaId == StatusConta.VencidaId).SumAsync(x => (decimal?)x.ValorLiquido, ct) ?? 0m);
        var qtdPendentes = await pendentesPagar.CountAsync(ct) + await pendentesReceber.CountAsync(ct);
        var qtdVencidos = await contasPagar.CountAsync(x => x.StatusContaId == StatusConta.VencidaId, ct) + await contasReceber.CountAsync(x => x.StatusContaId == StatusConta.VencidaId, ct);
        var qtdSemCategoria = await contasPagar.CountAsync(x => !db.RateiosContaGerencial.Any(r => r.ContaPagarId == x.Id), ct)
            + await contasReceber.CountAsync(x => !db.RateiosContaGerencial.Any(r => r.ContaReceberId == x.Id), ct);
        var qtdSemResponsavel = await contasPagar.CountAsync(x => x.ResponsavelCompraId == null, ct) + await contasReceber.CountAsync(x => x.ResponsavelId == null, ct);
        var qtdConciliacoesPendentes = await db.Conciliacoes.AsNoTracking().CountAsync(x => x.DataInicio <= fim && x.DataFim >= inicio && x.Status != StatusConciliacao.Concluida, ct);
        var qtdLancamentos = await contasPagar.CountAsync(ct) + await contasReceber.CountAsync(ct);

        var itens = new List<FechamentoMensalItemResponse>
        {
            Item("vencidos", "Lançamentos vencidos", qtdVencidos, totalVencido, "/agenda?status=VENCIDA", true),
            Item("pendentes", "Lançamentos pendentes", qtdPendentes, totalPendente, "/agenda?status=PENDENTE", true),
            Item("categorias", "Lançamentos sem conta gerencial", qtdSemCategoria, null, "/movimentacoes", true),
            Item("responsaveis", "Lançamentos sem responsável", qtdSemResponsavel, null, "/movimentacoes", true),
            Item("conciliacoes", "Conciliações em revisão", qtdConciliacoesPendentes, null, "/faturas", true),
        };
        var bloqueios = itens.Where(x => x.Bloqueante).Sum(x => x.Quantidade);
        var fechamento = await db.FechamentosMensais.AsNoTracking().SingleOrDefaultAsync(x => x.Competencia == competencia, ct);

        return new FechamentoMensalResponse(competencia, fechamento?.Status.ToString() ?? "Aberto", bloqueios == 0,
            bloqueios, totalReceitas, totalDespesas, totalReceitas - totalDespesas, totalPendente, totalVencido,
            qtdLancamentos, qtdSemCategoria, qtdSemResponsavel, qtdConciliacoesPendentes,
            fechamento?.FechadoPorUsuarioId, fechamento?.FechadoEmUtc, fechamento?.ReabertoPorUsuarioId,
            fechamento?.ReabertoEmUtc, fechamento?.JustificativaReabertura, itens);
    }

    private static FechamentoMensalItemResponse Item(string id, string titulo, int quantidade, decimal? valor, string rota, bool bloqueante) =>
        new(id, titulo, quantidade == 0 ? "Nenhuma pendência encontrada." : $"{quantidade} registro(s) exigem revisão.", quantidade == 0 ? "Ok" : "Pendente", bloqueante && quantidade > 0, quantidade, valor, rota);

    private Guid ObterUsuarioId() => Guid.TryParse(currentUser.UserId, out var id) && id != Guid.Empty
        ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");

    private static (DateOnly Inicio, DateOnly Fim) ObterPeriodo(string competencia)
    {
        if (!DateOnly.TryParseExact($"{competencia}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var inicio))
            throw new ArgumentException("Competência deve seguir o formato yyyy-MM.", nameof(competencia));
        return (inicio, inicio.AddMonths(1).AddDays(-1));
    }
}
