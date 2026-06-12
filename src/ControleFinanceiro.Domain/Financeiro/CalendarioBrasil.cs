namespace ControleFinanceiro.Domain.Financeiro;

public static class CalendarioBrasil
{
    private static readonly HashSet<string> FeriadosFixos =
    [
        "01-01", // Ano Novo
        "21-04", // Tiradentes
        "01-05", // Dia do Trabalho
        "07-09", // Independência
        "12-10", // Nossa Senhora Aparecida
        "02-11", // Finados
        "15-11", // Proclamação da República
        "25-12"  // Natal
    ];

    public static bool EhDiaUtil(DateOnly data)
    {
        if (data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        var chave = data.ToString("dd-MM");
        if (FeriadosFixos.Contains(chave))
        {
            return false;
        }

        // Feriados móveis (Cálculo da Páscoa)
        var pascoa = CalcularPascoa(data.Year);
        if (data == pascoa.AddDays(-47)) return false; // Carnaval (Terça)
        if (data == pascoa.AddDays(-48)) return false; // Segunda de Carnaval
        if (data == pascoa.AddDays(-2))  return false; // Sexta-feira Santa
        if (data == pascoa.AddDays(60))  return false; // Corpus Christi

        return true;
    }

    public static DateOnly ObterDiaUtil(int ano, int mes, int ordem)
    {
        var data = new DateOnly(ano, mes, 1);
        var diasUteisContados = 0;

        while (diasUteisContados < ordem)
        {
            if (EhDiaUtil(data))
            {
                diasUteisContados++;
                if (diasUteisContados == ordem)
                {
                    return data;
                }
            }

            data = data.AddDays(1);
            if (data.Month != mes)
            {
                // Se estourou o mês e não achou, retorna o último dia útil do mês (falha segura)
                return ObterUltimoDiaUtil(ano, mes);
            }
        }

        return data;
    }

    public static DateOnly ObterUltimoDiaUtil(int ano, int mes)
    {
        var data = new DateOnly(ano, mes, DateTime.DaysInMonth(ano, mes));
        while (!EhDiaUtil(data))
        {
            data = data.AddDays(-1);
        }
        return data;
    }

    private static DateOnly CalcularPascoa(int ano)
    {
        int a = ano % 19;
        int b = ano / 100;
        int c = ano % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int mes = (h + l - 7 * m + 114) / 31;
        int dia = ((h + l - 7 * m + 114) % 31) + 1;

        return new DateOnly(ano, mes, dia);
    }
}
