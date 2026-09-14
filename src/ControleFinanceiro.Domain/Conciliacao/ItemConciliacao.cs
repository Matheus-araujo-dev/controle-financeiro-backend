using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Conciliacao;

public sealed class ItemConciliacao : TenantEntity
{
    private ItemConciliacao() { }

    public Guid ConciliacaoId { get; private set; }
    public DateOnly Data { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public decimal Valor { get; private set; }
    public string? Documento { get; private set; }
    public StatusItemConciliacao StatusItem { get; private set; }
    public Guid? MovimentacaoVinculadaId { get; private set; }
    public Guid? SugestaoMovimentacaoId { get; private set; }
    public decimal? ScoreSugestao { get; private set; }

    public static ItemConciliacao Criar(
        DateOnly data,
        string descricao,
        decimal valor,
        string? documento)
    {
        return new ItemConciliacao
        {
            Data = data,
            Descricao = descricao.Trim(),
            Valor = valor,
            Documento = documento?.Trim(),
            StatusItem = StatusItemConciliacao.Pendente
        };
    }

    public void DefinirSugestao(Guid movimentacaoId, decimal score)
    {
        SugestaoMovimentacaoId = movimentacaoId;
        ScoreSugestao = score;
    }

    public void Conciliar(Guid? movimentacaoId)
    {
        MovimentacaoVinculadaId = movimentacaoId;
        StatusItem = StatusItemConciliacao.Conciliado;
    }

    public void Ignorar()
    {
        StatusItem = StatusItemConciliacao.Ignorado;
    }
}