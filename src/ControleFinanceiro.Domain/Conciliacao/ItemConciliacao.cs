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

    public Guid? ContaPagarVinculadaId { get; private set; }
    public decimal? ValorAnteriorSistema { get; private set; }
    public string? ChaveOrigem { get; private set; }
    public int NumeroParcela { get; private set; } = 1;
    public int QuantidadeParcelas { get; private set; } = 1;
    public string? RascunhoJson { get; private set; }

    public static ItemConciliacao CriarFatura(DateOnly data, string descricao, decimal valor, string chaveOrigem, int numeroParcela, int quantidadeParcelas)
    {
        if (string.IsNullOrWhiteSpace(chaveOrigem) || string.IsNullOrWhiteSpace(descricao) || valor == 0 || decimal.Round(valor, 2) != valor)
            throw new ArgumentException("Item de fatura inválido.");
        if (numeroParcela < 1 || quantidadeParcelas < numeroParcela || quantidadeParcelas > 120)
            throw new ArgumentException("Parcelamento inválido.");
        var item = Criar(data, descricao, valor, null);
        item.ChaveOrigem = chaveOrigem; item.NumeroParcela = numeroParcela; item.QuantidadeParcelas = quantidadeParcelas;
        return item;
    }

    public void SalvarRascunho(string json)
    {
        if (StatusItem != StatusItemConciliacao.Pendente) throw new InvalidOperationException("Item já processado.");
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Rascunho obrigatório.");
        RascunhoJson = json;
    }

    public void ConciliarContaPagar(Guid contaId, decimal? valorAnterior)
    {
        if (contaId == Guid.Empty || ChaveOrigem is null) throw new ArgumentException("Vínculo inválido.");
        if (StatusItem == StatusItemConciliacao.Conciliado && ContaPagarVinculadaId == contaId) return;
        if (StatusItem != StatusItemConciliacao.Pendente) throw new InvalidOperationException("Item já processado.");
        ContaPagarVinculadaId = contaId;
        ValorAnteriorSistema = valorAnterior;
        StatusItem = StatusItemConciliacao.Conciliado;
    }

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
        if (ContaPagarVinculadaId.HasValue) throw new InvalidOperationException("Não é possível ignorar um item já vinculado.");
        StatusItem = StatusItemConciliacao.Ignorado;
    }
}