namespace ControleFinanceiro.Application.Common.Exceptions;

public sealed class FaturaIndisponivelException(string competencia, bool faturaEstaPaga)
    : Exception(FormatMessage(competencia, faturaEstaPaga))
{
    private static string FormatMessage(string competencia, bool faturaEstaPaga)
    {
        var parts = competencia.Split('-');
        var formatted = parts.Length == 2 ? $"{parts[1]}/{parts[0]}" : competencia;
        var status = faturaEstaPaga ? "liquidada" : "fechada";
        return $"A fatura de {formatted} já está {status}. Deseja descartar o lançamento ou incluí-lo na próxima fatura em aberto?";
    }
}
