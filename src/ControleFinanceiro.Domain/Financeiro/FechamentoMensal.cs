using System.Globalization;
using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Financeiro;

public sealed class FechamentoMensal : TenantEntity
{
    private FechamentoMensal() { }

    public string Competencia { get; private set; } = string.Empty;
    public StatusFechamentoMensal Status { get; private set; }
    public Guid FechadoPorUsuarioId { get; private set; }
    public DateTimeOffset FechadoEmUtc { get; private set; }
    public decimal TotalReceitasSnapshot { get; private set; }
    public decimal TotalDespesasSnapshot { get; private set; }
    public decimal SaldoSnapshot { get; private set; }
    public decimal TotalPendenteSnapshot { get; private set; }
    public decimal TotalVencidoSnapshot { get; private set; }
    public int QuantidadeLancamentosSnapshot { get; private set; }
    public int QuantidadeBloqueiosSnapshot { get; private set; }
    public Guid? ReabertoPorUsuarioId { get; private set; }
    public DateTimeOffset? ReabertoEmUtc { get; private set; }
    public string? JustificativaReabertura { get; private set; }

    public static FechamentoMensal Fechar(
        string competencia,
        Guid responsavelId,
        DateTimeOffset fechadoEmUtc,
        decimal totalReceitas,
        decimal totalDespesas,
        decimal totalPendente,
        decimal totalVencido,
        int quantidadeLancamentos,
        int quantidadeBloqueios)
    {
        ValidarCompetencia(competencia);
        if (responsavelId == Guid.Empty) throw new ArgumentException("Responsável é obrigatório.", nameof(responsavelId));
        if (quantidadeBloqueios > 0) throw new InvalidOperationException("O mês possui bloqueios e não pode ser fechado.");
        if (quantidadeLancamentos < 0) throw new ArgumentOutOfRangeException(nameof(quantidadeLancamentos));
        if (quantidadeBloqueios < 0) throw new ArgumentOutOfRangeException(nameof(quantidadeBloqueios));

        return new FechamentoMensal
        {
            Competencia = competencia,
            Status = StatusFechamentoMensal.Fechado,
            FechadoPorUsuarioId = responsavelId,
            FechadoEmUtc = fechadoEmUtc,
            TotalReceitasSnapshot = decimal.Round(totalReceitas, 2),
            TotalDespesasSnapshot = decimal.Round(totalDespesas, 2),
            SaldoSnapshot = decimal.Round(totalReceitas - totalDespesas, 2),
            TotalPendenteSnapshot = decimal.Round(totalPendente, 2),
            TotalVencidoSnapshot = decimal.Round(totalVencido, 2),
            QuantidadeLancamentosSnapshot = quantidadeLancamentos,
            QuantidadeBloqueiosSnapshot = quantidadeBloqueios,
        };
    }

    public void Reabrir(Guid responsavelId, DateTimeOffset reabertoEmUtc, string justificativa)
    {
        if (Status != StatusFechamentoMensal.Fechado)
            throw new InvalidOperationException("Apenas um mês fechado pode ser reaberto.");
        if (responsavelId == Guid.Empty) throw new ArgumentException("Responsável é obrigatório.", nameof(responsavelId));
        if (string.IsNullOrWhiteSpace(justificativa))
            throw new ArgumentException("Justificativa de reabertura é obrigatória.", nameof(justificativa));
        if (justificativa.Trim().Length > 500)
            throw new ArgumentException("Justificativa de reabertura excede 500 caracteres.", nameof(justificativa));

        Status = StatusFechamentoMensal.Reaberto;
        ReabertoPorUsuarioId = responsavelId;
        ReabertoEmUtc = reabertoEmUtc;
        JustificativaReabertura = justificativa.Trim();
    }

    public void FecharNovamente(
        Guid responsavelId,
        DateTimeOffset fechadoEmUtc,
        decimal totalReceitas,
        decimal totalDespesas,
        decimal totalPendente,
        decimal totalVencido,
        int quantidadeLancamentos,
        int quantidadeBloqueios)
    {
        if (Status != StatusFechamentoMensal.Reaberto)
            throw new InvalidOperationException("Apenas um mês reaberto pode ser fechado novamente.");
        if (responsavelId == Guid.Empty) throw new ArgumentException("Responsável é obrigatório.", nameof(responsavelId));
        if (quantidadeBloqueios > 0) throw new InvalidOperationException("O mês possui bloqueios e não pode ser fechado.");
        if (quantidadeLancamentos < 0) throw new ArgumentOutOfRangeException(nameof(quantidadeLancamentos));
        if (quantidadeBloqueios < 0) throw new ArgumentOutOfRangeException(nameof(quantidadeBloqueios));

        Status = StatusFechamentoMensal.Fechado;
        FechadoPorUsuarioId = responsavelId;
        FechadoEmUtc = fechadoEmUtc;
        TotalReceitasSnapshot = decimal.Round(totalReceitas, 2);
        TotalDespesasSnapshot = decimal.Round(totalDespesas, 2);
        SaldoSnapshot = decimal.Round(totalReceitas - totalDespesas, 2);
        TotalPendenteSnapshot = decimal.Round(totalPendente, 2);
        TotalVencidoSnapshot = decimal.Round(totalVencido, 2);
        QuantidadeLancamentosSnapshot = quantidadeLancamentos;
        QuantidadeBloqueiosSnapshot = quantidadeBloqueios;
        ReabertoPorUsuarioId = null;
        ReabertoEmUtc = null;
        JustificativaReabertura = null;
    }

    private static void ValidarCompetencia(string competencia)
    {
        if (string.IsNullOrWhiteSpace(competencia)
            || !DateOnly.TryParseExact(
                $"{competencia}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            throw new ArgumentException("Competência deve seguir o formato yyyy-MM.", nameof(competencia));
        }
    }
}
