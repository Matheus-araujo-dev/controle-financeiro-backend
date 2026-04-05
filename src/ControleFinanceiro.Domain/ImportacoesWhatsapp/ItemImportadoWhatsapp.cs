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
}
