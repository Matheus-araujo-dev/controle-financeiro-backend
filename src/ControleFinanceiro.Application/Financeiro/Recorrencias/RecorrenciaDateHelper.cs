namespace ControleFinanceiro.Application.Financeiro.Recorrencias;

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
}
