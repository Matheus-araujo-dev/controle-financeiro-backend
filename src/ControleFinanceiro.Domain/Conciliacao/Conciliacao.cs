using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Conciliacao;

public sealed class Conciliacao : TenantEntity
{
    private readonly List<ItemConciliacao> _itens = [];

    private Conciliacao() { }

    public string NomeArquivo { get; private set; } = string.Empty;
    public FormatoArquivo Formato { get; private set; }
    public Guid? ContaBancariaId { get; private set; }
    public Guid? FaturaId { get; private set; }
    public string? HashArquivo { get; private set; }
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

    public static Conciliacao CriarFatura(string nomeArquivo, Guid faturaId, string hashArquivo, IReadOnlyList<ItemConciliacao> itens)
    {
        if (faturaId == Guid.Empty || string.IsNullOrWhiteSpace(nomeArquivo) || string.IsNullOrWhiteSpace(hashArquivo) || itens.Count == 0)
            throw new ArgumentException("Fatura, arquivo e itens são obrigatórios.");
        if (itens.Any(i => string.IsNullOrWhiteSpace(i.ChaveOrigem)) || itens.Select(i => i.ChaveOrigem).Distinct().Count() != itens.Count)
            throw new ArgumentException("Os itens da fatura precisam de chaves independentes.");
        var session = new Conciliacao { NomeArquivo = nomeArquivo.Trim(), FaturaId = faturaId, HashArquivo = hashArquivo,
            Formato = FormatoArquivo.Pdf, DataInicio = itens.Min(i => i.Data), DataFim = itens.Max(i => i.Data),
            TotalItens = itens.Count, Status = StatusConciliacao.EmRevisao };
        session._itens.AddRange(itens);
        return session;
    }

    public void ConciliarContaPagar(Guid itemId, Guid contaId, decimal? valorAnterior)
    {
        if (!FaturaId.HasValue) throw new InvalidOperationException("Esta conciliação é de extrato bancário.");
        if (_itens.Any(i => i.Id != itemId && i.ContaPagarVinculadaId == contaId))
            throw new InvalidOperationException("Esta conta já foi vinculada a outro item desta fatura.");
        var item = _itens.SingleOrDefault(i => i.Id == itemId) ?? throw new InvalidOperationException("Item não encontrado.");
        item.ConciliarContaPagar(contaId, valorAnterior);
        AtualizarProgresso();
    }

    public void SalvarRascunho(Guid itemId, string json)
    {
        if (!FaturaId.HasValue) throw new InvalidOperationException("Esta conciliação é de extrato bancário.");
        var item = _itens.SingleOrDefault(i => i.Id == itemId) ?? throw new InvalidOperationException("Item não encontrado.");
        item.SalvarRascunho(json);
    }

    private void AtualizarProgresso()
    {
        ItensConciliados = _itens.Count(i => i.StatusItem != StatusItemConciliacao.Pendente);
        Status = ItensConciliados == TotalItens ? StatusConciliacao.Concluida : StatusConciliacao.EmRevisao;
    }

    public void ConciliarItem(Guid itemId, Guid? movimentacaoId)
    {
        var item = _itens.SingleOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Item nao encontrado.");
        if (FaturaId.HasValue) throw new InvalidOperationException("Use o vínculo de conta a pagar para faturas.");
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