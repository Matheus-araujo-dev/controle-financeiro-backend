using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Conciliacao;

public sealed class Conciliacao : TenantEntity
{
    private readonly List<ItemConciliacao> _itens = [];

    private Conciliacao() { }

    public string NomeArquivo { get; private set; } = string.Empty;
    public FormatoArquivo Formato { get; private set; }
    public Guid ContaBancariaId { get; private set; }
    public DateOnly DataInicio { get; private set; }
    public DateOnly DataFim { get; private set; }
    public int TotalItens { get; private set; }
    public int ItensConciliados { get; private set; }
    public StatusConciliacao Status { get; private set; }
    public IReadOnlyCollection<ItemConciliacao> Itens => _itens;

    public static Conciliacao Criar(
        string nomeArquivo,
        FormatoArquivo formato,
        Guid contaBancariaId,
        DateOnly dataInicio,
        DateOnly dataFim,
        IReadOnlyList<ItemConciliacao> itens)
    {
        if (string.IsNullOrWhiteSpace(nomeArquivo)) throw new ArgumentException("Nome do arquivo obrigatorio.", nameof(nomeArquivo));
        if (contaBancariaId == Guid.Empty) throw new ArgumentException("Conta bancaria obrigatoria.", nameof(contaBancariaId));
        if (itens.Count == 0) throw new ArgumentException("O arquivo deve conter pelo menos um item.", nameof(itens));

        var conciliacao = new Conciliacao
        {
            NomeArquivo = nomeArquivo.Trim(),
            Formato = formato,
            ContaBancariaId = contaBancariaId,
            DataInicio = dataInicio,
            DataFim = dataFim,
            TotalItens = itens.Count,
            ItensConciliados = 0,
            Status = StatusConciliacao.EmRevisao
        };
        conciliacao._itens.AddRange(itens);
        return conciliacao;
    }

    public void ConciliarItem(Guid itemId, Guid? movimentacaoId)
    {
        var item = _itens.SingleOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Item nao encontrado.");
        item.Conciliar(movimentacaoId);
        ItensConciliados = _itens.Count(i => i.StatusItem != StatusItemConciliacao.Pendente);
        if (ItensConciliados == TotalItens) Status = StatusConciliacao.Concluida;
    }

    public void IgnorarItem(Guid itemId)
    {
        var item = _itens.SingleOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Item nao encontrado.");
        item.Ignorar();
        ItensConciliados = _itens.Count(i => i.StatusItem != StatusItemConciliacao.Pendente);
        if (ItensConciliados == TotalItens) Status = StatusConciliacao.Concluida;
    }
}