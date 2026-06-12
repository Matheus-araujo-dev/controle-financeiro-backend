using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Financeiro;

public sealed class RegraRecorrencia : TenantEntity
{
    private RegraRecorrencia()
    {
    }

    public TipoLancamentoRecorrencia TipoLancamento { get; private set; }

    public TipoPeriodicidadeRecorrencia TipoPeriodicidade { get; private set; }

    public TipoDiaRecorrencia TipoDia { get; private set; }

    public int DiaOrdemMensal { get; private set; }

    public DateOnly DataInicio { get; private set; }

    public DateOnly? DataFim { get; private set; }

    public bool Ativa { get; private set; }

    public bool PermiteEdicaoOcorrenciaIndividual { get; private set; }

    public string? Observacao { get; private set; }

    public string TemplateJson { get; private set; } = string.Empty;

    public static RegraRecorrencia Criar(
        TipoLancamentoRecorrencia tipoLancamento,
        TipoPeriodicidadeRecorrencia tipoPeriodicidade,
        TipoDiaRecorrencia tipoDia,
        int diaOrdemMensal,
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
            tipoDia,
            diaOrdemMensal,
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
        TipoDiaRecorrencia tipoDia,
        int diaOrdemMensal,
        DateOnly dataInicio,
        DateOnly? dataFim,
        bool permiteEdicaoOcorrenciaIndividual,
        string? observacao,
        string templateJson)
    {
        DefinirCampos(
            TipoLancamento,
            tipoPeriodicidade,
            tipoDia,
            diaOrdemMensal,
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
        if (Ativa) return;

        Ativa = true;
    }

    public void Encerrar(DateOnly dataFim)
    {
        if (dataFim < DataInicio)
        {
            throw new ArgumentException("Data fim deve ser maior ou igual à data de início.", nameof(dataFim));
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
        
        // Começamos do mês da DataInicio
        var dataReferencia = new DateOnly(DataInicio.Year, DataInicio.Month, 1);
        var dataLimiteReferencia = new DateOnly(dataLimite.Year, dataLimite.Month, 1);

        while (dataReferencia <= dataLimiteReferencia)
        {
            var dataOcorrencia = CalcularDataParaMes(dataReferencia.Year, dataReferencia.Month);
            
            if (dataOcorrencia >= DataInicio && dataOcorrencia <= dataLimite && !datasExistentesSet.Contains(dataOcorrencia))
            {
                datas.Add(dataOcorrencia);
            }

            dataReferencia = dataReferencia.AddMonths(1);
        }

        return datas;
    }

    private void DefinirCampos(
        TipoLancamentoRecorrencia tipoLancamento,
        TipoPeriodicidadeRecorrencia tipoPeriodicidade,
        TipoDiaRecorrencia tipoDia,
        int diaOrdemMensal,
        DateOnly dataInicio,
        DateOnly? dataFim,
        bool ativa,
        bool permiteEdicaoOcorrenciaIndividual,
        string? observacao,
        string templateJson)
    {
        if (tipoPeriodicidade != TipoPeriodicidadeRecorrencia.Mensal)
        {
            throw new ArgumentOutOfRangeException(nameof(tipoPeriodicidade), "Periodicidade não suportada.");
        }

        if (diaOrdemMensal is < 1 or > 31)
        {
            throw new ArgumentException("Dia de ordem mensal inválido.", nameof(diaOrdemMensal));
        }

        if (dataFim.HasValue && dataFim.Value < dataInicio)
        {
            throw new ArgumentException("Data fim deve ser maior ou igual à data de início.", nameof(dataFim));
        }

        if (string.IsNullOrWhiteSpace(templateJson))
        {
            throw new ArgumentException("Template da recorrência é obrigatório.", nameof(templateJson));
        }

        TipoLancamento = tipoLancamento;
        TipoPeriodicidade = tipoPeriodicidade;
        TipoDia = tipoDia;
        DiaOrdemMensal = diaOrdemMensal;
        DataInicio = dataInicio;
        DataFim = dataFim;
        Ativa = ativa;
        PermiteEdicaoOcorrenciaIndividual = permiteEdicaoOcorrenciaIndividual;
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        TemplateJson = templateJson;
    }

    public DateOnly CalcularDataParaMes(int ano, int mes)
    {
        return TipoDia == TipoDiaRecorrencia.DiaUtil
            ? CalendarioBrasil.ObterDiaUtil(ano, mes, DiaOrdemMensal)
            : AjustarParaDiaFixo(ano, mes, DiaOrdemMensal);
    }

    private static DateOnly AjustarParaDiaFixo(int year, int month, int dia)
    {
        var ultimoDia = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, Math.Min(dia, ultimoDia));
    }

}
