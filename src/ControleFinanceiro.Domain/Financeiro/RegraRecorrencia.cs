using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Financeiro;

public sealed class RegraRecorrencia : AuditableEntity
{
    private RegraRecorrencia()
    {
    }

    public TipoLancamentoRecorrencia TipoLancamento { get; private set; }

    public TipoPeriodicidadeRecorrencia TipoPeriodicidade { get; private set; }

    public int DiaGeracaoMensal { get; private set; }

    public DateOnly DataInicio { get; private set; }

    public DateOnly? DataFim { get; private set; }

    public bool Ativa { get; private set; }

    public bool PermiteEdicaoOcorrenciaIndividual { get; private set; }

    public string? Observacao { get; private set; }

    public string TemplateJson { get; private set; } = string.Empty;

    public static RegraRecorrencia Criar(
        TipoLancamentoRecorrencia tipoLancamento,
        TipoPeriodicidadeRecorrencia tipoPeriodicidade,
        int diaGeracaoMensal,
        DateOnly dataInicio,
        DateOnly? dataFim,
        bool permiteEdicaoOcorrenciaIndividual,
        string? observacao,
        string templateJson)
    {
        var regra = new RegraRecorrencia();
        regra.DefinirCampos(
            tipoLancamento,
            tipoPeriodicidade,
            diaGeracaoMensal,
            dataInicio,
            dataFim,
            ativa: true,
            permiteEdicaoOcorrenciaIndividual,
            observacao,
            templateJson);
        return regra;
    }

    public void Atualizar(
        TipoPeriodicidadeRecorrencia tipoPeriodicidade,
        int diaGeracaoMensal,
        DateOnly dataInicio,
        DateOnly? dataFim,
        bool permiteEdicaoOcorrenciaIndividual,
        string? observacao,
        string templateJson)
    {
        DefinirCampos(
            TipoLancamento,
            tipoPeriodicidade,
            diaGeracaoMensal,
            dataInicio,
            dataFim,
            Ativa,
            permiteEdicaoOcorrenciaIndividual,
            observacao,
            templateJson);
    }

    public void Pausar()
    {
        Ativa = false;
    }

    public void Retomar()
    {
        if (DataFim.HasValue && DataFim.Value < DataInicio)
        {
            throw new InvalidOperationException("Nao e permitido retomar uma recorrencia inconsistente.");
        }

        Ativa = true;
    }

    public void Encerrar(DateOnly dataFim)
    {
        if (dataFim < DataInicio)
        {
            throw new ArgumentException("Data fim deve ser maior ou igual a data de inicio.", nameof(dataFim));
        }

        DataFim = dataFim;
        Ativa = false;
    }

    public IReadOnlyCollection<DateOnly> CalcularDatasPendentes(
        IReadOnlyCollection<DateOnly> datasExistentes,
        DateOnly ateData)
    {
        if (!Ativa || ateData < DataInicio)
        {
            return [];
        }

        var dataLimite = DataFim.HasValue && DataFim.Value < ateData
            ? DataFim.Value
            : ateData;

        var datas = new List<DateOnly>();
        var datasExistentesSet = datasExistentes.ToHashSet();
        var dataAtual = DataInicio;

        while (dataAtual <= dataLimite)
        {
            if (!datasExistentesSet.Contains(dataAtual))
            {
                datas.Add(dataAtual);
            }

            dataAtual = Avancar(dataAtual);
        }

        return datas;
    }

    private void DefinirCampos(
        TipoLancamentoRecorrencia tipoLancamento,
        TipoPeriodicidadeRecorrencia tipoPeriodicidade,
        int diaGeracaoMensal,
        DateOnly dataInicio,
        DateOnly? dataFim,
        bool ativa,
        bool permiteEdicaoOcorrenciaIndividual,
        string? observacao,
        string templateJson)
    {
        if (tipoPeriodicidade != TipoPeriodicidadeRecorrencia.Mensal)
        {
            throw new ArgumentOutOfRangeException(nameof(tipoPeriodicidade), "Periodicidade nao suportada.");
        }

        if (diaGeracaoMensal is < 1 or > 31)
        {
            throw new ArgumentException("Dia de geracao mensal invalido.", nameof(diaGeracaoMensal));
        }

        if (dataFim.HasValue && dataFim.Value < dataInicio)
        {
            throw new ArgumentException("Data fim deve ser maior ou igual a data de inicio.", nameof(dataFim));
        }

        if (string.IsNullOrWhiteSpace(templateJson))
        {
            throw new ArgumentException("Template da recorrencia e obrigatorio.", nameof(templateJson));
        }

        TipoLancamento = tipoLancamento;
        TipoPeriodicidade = tipoPeriodicidade;
        DiaGeracaoMensal = diaGeracaoMensal;
        DataInicio = AjustarParaDia(dataInicio.Year, dataInicio.Month, diaGeracaoMensal);
        DataFim = dataFim;
        Ativa = ativa;
        PermiteEdicaoOcorrenciaIndividual = permiteEdicaoOcorrenciaIndividual;
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        TemplateJson = templateJson;
    }

    private DateOnly Avancar(DateOnly dataAtual)
    {
        var proximoMes = dataAtual.AddMonths(1);
        return AjustarParaDia(proximoMes.Year, proximoMes.Month, DiaGeracaoMensal);
    }

    private static DateOnly AjustarParaDia(int year, int month, int dia)
    {
        var ultimoDia = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, Math.Min(dia, ultimoDia));
    }
}
