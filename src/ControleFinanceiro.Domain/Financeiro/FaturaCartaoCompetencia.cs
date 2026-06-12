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

        // Regra de fechamento: compras ANTES do dia de fechamento entram na fatura
        // corrente; compras no dia de fechamento em diante entram na próxima.
        // Ex.: fecha dia 10 → compra dia 09 entra na fatura do mês; compra dia 10 vai para a próxima.
        var competenciaBase = new DateOnly(dataCompra.Year, dataCompra.Month, 1);
        if (dataCompra.Day >= diaFechamento)
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

    public static Resultado CalcularPorDataVencimento(DateOnly dataVencimento, int diaFechamento, int diaVencimento)
    {
        if (diaFechamento < 1 || diaFechamento > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(diaFechamento), "Dia de fechamento deve ficar entre 1 e 31.");
        }

        if (diaVencimento < 1 || diaVencimento > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(diaVencimento), "Dia de vencimento deve ficar entre 1 e 31.");
        }

        var competenciaBase = new DateOnly(dataVencimento.Year, dataVencimento.Month, 1);
        if (diaVencimento <= diaFechamento)
        {
            competenciaBase = competenciaBase.AddMonths(-1);
        }

        var dataFechamento = CriarDataSegura(competenciaBase.Year, competenciaBase.Month, diaFechamento);

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
