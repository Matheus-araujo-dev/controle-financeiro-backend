namespace ControleFinanceiro.Domain.Financeiro;

public static class FaturaCartaoCompetencia
{
    public static Resultado Calcular(DateOnly dataCompra, int diaFechamento, int diaVencimento)
    {
        if (diaFechamento < 1 || diaFechamento > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(diaFechamento), "Dia de fechamento deve ficar entre 1 e 31.");
        }

        if (diaVencimento < 1 || diaVencimento > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(diaVencimento), "Dia de vencimento deve ficar entre 1 e 31.");
        }

        var competenciaBase = new DateOnly(dataCompra.Year, dataCompra.Month, 1);
        if (dataCompra.Day > diaFechamento)
        {
            competenciaBase = competenciaBase.AddMonths(1);
        }

        var dataFechamento = CriarDataSegura(competenciaBase.Year, competenciaBase.Month, diaFechamento);
        var dataVencimento = CriarDataSegura(competenciaBase.Year, competenciaBase.Month, diaVencimento);

        if (dataVencimento <= dataFechamento)
        {
            var proximoMes = competenciaBase.AddMonths(1);
            dataVencimento = CriarDataSegura(proximoMes.Year, proximoMes.Month, diaVencimento);
        }

        return new Resultado(
            $"{competenciaBase.Year:D4}-{competenciaBase.Month:D2}",
            dataFechamento,
            dataVencimento);
    }

    private static DateOnly CriarDataSegura(int ano, int mes, int dia)
    {
        return new DateOnly(ano, mes, Math.Min(dia, DateTime.DaysInMonth(ano, mes)));
    }

    public sealed record Resultado(string Competencia, DateOnly DataFechamento, DateOnly DataVencimento);
}
