using ControleFinanceiro.Domain.Events;

namespace ControleFinanceiro.Domain.Financeiro.Events;

public sealed class ContaPagarCriadaEvent : DomainEventBase
{
    public Guid ContaPagarId { get; }

    public string NumeroDocumento { get; }

    public Guid RecebedorId { get; }

    public string Descricao { get; }

    public decimal ValorLiquido { get; }

    public DateOnly DataVencimento { get; }

    public int QuantidadeParcelas { get; }

    public int NumeroParcela { get; }

    public Guid? GrupoParcelamentoId { get; }

    public bool EhRecorrente { get; }

    public Guid? RegraRecorrenciaId { get; }

    public ContaPagarCriadaEvent(
        Guid contaPagarId,
        string? numeroDocumento,
        Guid recebedorId,
        string descricao,
        decimal valorLiquido,
        DateOnly dataVencimento,
        int quantidadeParcelas,
        int numeroParcela,
        Guid? grupoParcelamentoId,
        bool ehRecorrente,
        Guid? regraRecorrenciaId)
    {
        ContaPagarId = contaPagarId;
        NumeroDocumento = numeroDocumento ?? string.Empty;
        RecebedorId = recebedorId;
        Descricao = descricao;
        ValorLiquido = valorLiquido;
        DataVencimento = dataVencimento;
        QuantidadeParcelas = quantidadeParcelas;
        NumeroParcela = numeroParcela;
        GrupoParcelamentoId = grupoParcelamentoId;
        EhRecorrente = ehRecorrente;
        RegraRecorrenciaId = regraRecorrenciaId;
    }
}