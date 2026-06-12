namespace ControleFinanceiro.Application.Financeiro.Recorrencias;

using ControleFinanceiro.Domain.Financeiro;

internal static class RecorrenciaDateHelper
{
    public static int CalculateMonthOffset(DateOnly sourceDate, DateOnly targetDate)
    {
        return ((targetDate.Year - sourceDate.Year) * 12) + targetDate.Month - sourceDate.Month;
    }

    public static DateOnly Shift(DateOnly sourceDate, int monthOffset)
    {
        return sourceDate.AddMonths(monthOffset);
    }

    public static DateOnly CalculateAutomaticStartDate(
        DateOnly dataEmissao,
        TipoDiaRecorrencia tipoDia,
        int diaOrdemMensal)
    {
        var referencia = new DateOnly(dataEmissao.Year, dataEmissao.Month, 1).AddMonths(1);

        return CalculateDateForReferenceMonth(referencia, tipoDia, diaOrdemMensal);
    }

    public static DateOnly CalculateDateForReferenceMonth(
        DateOnly referencia,
        TipoDiaRecorrencia tipoDia,
        int diaOrdemMensal)
    {
        return tipoDia == TipoDiaRecorrencia.DiaUtil
            ? CalendarioBrasil.ObterDiaUtil(referencia.Year, referencia.Month, diaOrdemMensal)
            : new DateOnly(referencia.Year, referencia.Month, Math.Min(diaOrdemMensal, DateTime.DaysInMonth(referencia.Year, referencia.Month)));
    }
}
