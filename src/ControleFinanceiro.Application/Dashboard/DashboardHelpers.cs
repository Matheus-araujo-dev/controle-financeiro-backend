using System.Globalization;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Financeiro;

namespace ControleFinanceiro.Application.Dashboard;

internal static class DashboardHelpers
{
    internal const int MaxObservacaoLength = 50;

    internal static readonly IReadOnlyDictionary<Guid, StatusContaInfo> StatusContasLookup =
        StatusConta.Seeds().ToDictionary(s => s.Id, s => new StatusContaInfo(s.Codigo, s.Nome));

    internal static (DateOnly DataInicial, int Dias, bool MesReferenciaEhMesAtual) ResolverJanela(
        string? mesReferencia,
        DateOnly? dataInicial,
        int dias)
    {
        if (!string.IsNullOrWhiteSpace(mesReferencia))
        {
            var inicioMes = ParseMesReferencia(mesReferencia);
            var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
            return (
                inicioMes,
                DateTime.DaysInMonth(inicioMes.Year, inicioMes.Month),
                inicioMes.Year == hoje.Year && inicioMes.Month == hoje.Month);
        }

        if (dias < 1)
            throw ValidationExceptionFactory.Create("Dias", "Quantidade de dias deve ser maior que zero.");

        return (dataInicial ?? DateOnly.FromDateTime(DateTime.UtcNow), dias, false);
    }

    internal static DateOnly ParseMesReferencia(string mesReferencia)
    {
        if (!DateOnly.TryParseExact(
                $"{mesReferencia.Trim()}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var inicioMes))
        {
            throw ValidationExceptionFactory.Create("MesReferencia", "Mês de referência inválido. Use o formato yyyy-MM.");
        }

        return inicioMes;
    }

    internal static TipoContaGerencial? ParseTipoContaGerencial(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo)) return null;

        if (Enum.TryParse<TipoContaGerencial>(tipo, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;

        throw ValidationExceptionFactory.Create("Tipo", "Tipo de conta gerencial inválido. Use Receita ou Despesa.");
    }

    internal static string? TruncarObservacao(string? observacao)
    {
        if (string.IsNullOrWhiteSpace(observacao)) return null;
        return observacao.Length <= MaxObservacaoLength ? observacao : $"{observacao[..MaxObservacaoLength]}...";
    }

    internal static Contracts.Financeiro.Movimentacoes.TipoMovimentacaoResponse MapearTipoMovimentacao(TipoMovimentacao tipo) =>
        tipo switch
        {
            TipoMovimentacao.Entrada => Contracts.Financeiro.Movimentacoes.TipoMovimentacaoResponse.Entrada,
            TipoMovimentacao.Saida => Contracts.Financeiro.Movimentacoes.TipoMovimentacaoResponse.Saida,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };

    internal static Contracts.Financeiro.Movimentacoes.NaturezaMovimentacaoResponse MapearNaturezaMovimentacao(NaturezaMovimentacao natureza) =>
        natureza switch
        {
            NaturezaMovimentacao.Realizada => Contracts.Financeiro.Movimentacoes.NaturezaMovimentacaoResponse.Realizada,
            NaturezaMovimentacao.Prevista => Contracts.Financeiro.Movimentacoes.NaturezaMovimentacaoResponse.Prevista,
            _ => throw new ArgumentOutOfRangeException(nameof(natureza))
        };

    internal static IEnumerable<DateOnly> CalcularDatasProjetadas(
        Domain.Financeiro.RegraRecorrencia regra,
        HashSet<DateOnly> mesesComOcorrencia,
        DateOnly dataInicial,
        DateOnly dataFinal)
    {
        var mes = new DateOnly(dataInicial.Year, dataInicial.Month, 1);
        var mesFinal = new DateOnly(dataFinal.Year, dataFinal.Month, 1);

        while (mes <= mesFinal)
        {
            if (!mesesComOcorrencia.Contains(mes))
            {
                var data = regra.CalcularDataParaMes(mes.Year, mes.Month);
                if (data >= dataInicial && data <= dataFinal &&
                    data >= regra.DataInicio &&
                    (!regra.DataFim.HasValue || data <= regra.DataFim.Value))
                {
                    yield return data;
                }
            }

            mes = mes.AddMonths(1);
        }
    }

    internal static IEnumerable<(ImportacaoCompraInfo Compra, DateOnly Data)> ProjetarComprasImportadas(
        IReadOnlyCollection<ImportacaoCompraInfo> compras,
        DateOnly dataInicial,
        DateOnly dataFinal,
        bool usarDataVencimento)
    {
        DateOnly DataDe(ImportacaoCompraInfo c) => usarDataVencimento ? c.DataVencimento : c.DataCompra;

        foreach (var grupo in compras.Where(c => c.Recorrente).GroupBy(c => c.SerieRecorrenteKey ?? c.Id.ToString()))
        {
            var semente = grupo.OrderBy(DataDe).Last();
            var mesesExistentes = grupo.Select(c => new DateOnly(DataDe(c).Year, DataDe(c).Month, 1)).ToHashSet();

            for (var d = 1; ; d++)
            {
                var data = DataDe(semente).AddMonths(d);
                if (data > dataFinal) break;
                if (data >= dataInicial && !mesesExistentes.Contains(new DateOnly(data.Year, data.Month, 1)))
                    yield return (semente, data);
            }
        }

        foreach (var grupo in compras
                     .Where(c => !c.Recorrente && c.Parcelamento is not null)
                     .GroupBy(c => c.SerieParcelamentoKey ?? c.Id.ToString()))
        {
            var semente = grupo.OrderBy(c => c.Parcelamento!.NumeroParcela).Last();
            var mesesExistentes = grupo.Select(c => new DateOnly(DataDe(c).Year, DataDe(c).Month, 1)).ToHashSet();

            for (var parcela = semente.Parcelamento!.NumeroParcela + 1; parcela <= semente.Parcelamento.QuantidadeParcelas; parcela++)
            {
                var data = DataDe(semente).AddMonths(parcela - semente.Parcelamento.NumeroParcela);
                if (data > dataFinal) break;
                if (data >= dataInicial && !mesesExistentes.Contains(new DateOnly(data.Year, data.Month, 1)))
                    yield return (semente, data);
            }
        }
    }
}
