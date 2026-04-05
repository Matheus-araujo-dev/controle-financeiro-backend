using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.ImportacoesWhatsapp;

public sealed class ItemImportadoWhatsapp : AuditableEntity
{
    private ItemImportadoWhatsapp()
    {
    }

    public Guid ImportacaoWhatsappId { get; private set; }

    public TipoSugestaoImportacaoWhatsapp TipoSugestao { get; private set; }

    public string PayloadSugeridoJson { get; private set; } = string.Empty;

    public Guid? MovimentacaoFinanceiraId { get; private set; }

    public StatusItemImportadoWhatsapp Status { get; private set; }

    public string? Observacao { get; private set; }

    public DateTime? ConfirmadoEmUtc { get; private set; }

    public DateTime? RejeitadoEmUtc { get; private set; }

    public static ItemImportadoWhatsapp Criar(
        Guid importacaoWhatsappId,
        TipoSugestaoImportacaoWhatsapp tipoSugestao,
        string payloadSugeridoJson)
    {
        if (importacaoWhatsappId == Guid.Empty)
        {
            throw new ArgumentException("Importacao e obrigatoria.", nameof(importacaoWhatsappId));
        }

        if (string.IsNullOrWhiteSpace(payloadSugeridoJson))
        {
            throw new ArgumentException("Payload sugerido e obrigatorio.", nameof(payloadSugeridoJson));
        }

        return new ItemImportadoWhatsapp
        {
            ImportacaoWhatsappId = importacaoWhatsappId,
            TipoSugestao = tipoSugestao,
            PayloadSugeridoJson = payloadSugeridoJson.Trim(),
            Status = StatusItemImportadoWhatsapp.Sugerido
        };
    }

    public void Confirmar(string? observacao)
    {
        if (Status != StatusItemImportadoWhatsapp.Sugerido)
        {
            throw new InvalidOperationException("Somente itens sugeridos podem ser confirmados.");
        }

        Status = StatusItemImportadoWhatsapp.Confirmado;
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        ConfirmadoEmUtc = DateTime.UtcNow;
        RejeitadoEmUtc = null;
    }

    public void Rejeitar(string? observacao)
    {
        if (Status != StatusItemImportadoWhatsapp.Sugerido)
        {
            throw new InvalidOperationException("Somente itens sugeridos podem ser rejeitados.");
        }

        Status = StatusItemImportadoWhatsapp.Rejeitado;
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        RejeitadoEmUtc = DateTime.UtcNow;
        ConfirmadoEmUtc = null;
    }

    public void VincularMovimentacao(Guid movimentacaoFinanceiraId, string? observacao)
    {
        if (TipoSugestao != TipoSugestaoImportacaoWhatsapp.ItemExtrato)
        {
            throw new InvalidOperationException("Somente itens de extrato podem ser vinculados na conciliacao.");
        }

        if (Status != StatusItemImportadoWhatsapp.Confirmado)
        {
            throw new InvalidOperationException("Somente itens confirmados podem ser conciliados.");
        }

        if (movimentacaoFinanceiraId == Guid.Empty)
        {
            throw new ArgumentException("Movimentacao financeira e obrigatoria.", nameof(movimentacaoFinanceiraId));
        }

        if (MovimentacaoFinanceiraId.HasValue)
        {
            throw new InvalidOperationException("Item de extrato ja possui movimentacao conciliada.");
        }

        MovimentacaoFinanceiraId = movimentacaoFinanceiraId;
        Observacao = string.IsNullOrWhiteSpace(observacao) ? Observacao : observacao.Trim();
    }
}
