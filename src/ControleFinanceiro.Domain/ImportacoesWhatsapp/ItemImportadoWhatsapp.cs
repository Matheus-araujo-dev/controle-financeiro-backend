using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.ImportacoesWhatsapp;

public sealed class ItemImportadoWhatsapp : TenantEntity
{
    private ItemImportadoWhatsapp()
    {
    }

    public Guid ImportacaoWhatsappId { get; private set; }

    public TipoSugestaoImportacaoWhatsapp TipoSugestao { get; private set; }

    public string PayloadSugeridoJson { get; private set; } = string.Empty;

    public string? ChaveAprendizado { get; private set; }

    public Guid? ContaGerencialId { get; private set; }

    public Guid? ResponsavelId { get; private set; }

    public string? DescricaoAjustada { get; private set; }

    public bool MarcarComoRecorrente { get; private set; }

    public Guid? ContaReceberId { get; private set; }

    public StatusItemImportadoWhatsapp Status { get; private set; }

    public string? Observacao { get; private set; }

    public DateTime? ConfirmadoEmUtc { get; private set; }

    public DateTime? RejeitadoEmUtc { get; private set; }

    public static ItemImportadoWhatsapp Criar(
        Guid importacaoWhatsappId,
        TipoSugestaoImportacaoWhatsapp tipoSugestao,
        string payloadSugeridoJson,
        string? chaveAprendizado)
    {
        if (importacaoWhatsappId == Guid.Empty)
        {
            throw new ArgumentException("Importação é obrigatória.", nameof(importacaoWhatsappId));
        }

        if (string.IsNullOrWhiteSpace(payloadSugeridoJson))
        {
            throw new ArgumentException("Payload sugerido é obrigatório.", nameof(payloadSugeridoJson));
        }

        return new ItemImportadoWhatsapp
        {
            ImportacaoWhatsappId = importacaoWhatsappId,
            TipoSugestao = tipoSugestao,
            PayloadSugeridoJson = payloadSugeridoJson.Trim(),
            ChaveAprendizado = string.IsNullOrWhiteSpace(chaveAprendizado) ? null : chaveAprendizado.Trim(),
            Status = StatusItemImportadoWhatsapp.Sugerido
        };
    }

    public void Confirmar(
        string? observacao,
        string? descricaoAjustada,
        Guid? contaGerencialId,
        Guid? responsavelId,
        Guid? contaReceberId,
        bool marcarComoRecorrente)
    {
        if (Status != StatusItemImportadoWhatsapp.Sugerido)
        {
            throw new InvalidOperationException("Somente itens sugeridos podem ser confirmados.");
        }

        Status = StatusItemImportadoWhatsapp.Confirmado;
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        DescricaoAjustada = string.IsNullOrWhiteSpace(descricaoAjustada) ? null : descricaoAjustada.Trim();
        MarcarComoRecorrente = marcarComoRecorrente;
        ContaGerencialId = contaGerencialId;
        ResponsavelId = responsavelId;
        ContaReceberId = contaReceberId;
        ConfirmadoEmUtc = DateTime.UtcNow;
        RejeitadoEmUtc = null;
    }

    public void AtualizarConfirmacao(
        string? observacao,
        string? descricaoAjustada,
        Guid? contaGerencialId,
        Guid? responsavelId,
        Guid? contaReceberId,
        bool marcarComoRecorrente)
    {
        if (Status != StatusItemImportadoWhatsapp.Confirmado)
        {
            throw new InvalidOperationException("Somente itens confirmados podem ter a revisão atualizada.");
        }

        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        DescricaoAjustada = string.IsNullOrWhiteSpace(descricaoAjustada) ? null : descricaoAjustada.Trim();
        MarcarComoRecorrente = marcarComoRecorrente;
        ContaGerencialId = contaGerencialId;
        ResponsavelId = responsavelId;
        ContaReceberId = contaReceberId;
    }

    public void ReabrirParaEdicao()
    {
        if (Status == StatusItemImportadoWhatsapp.Sugerido)
        {
            return;
        }

        Status = StatusItemImportadoWhatsapp.Sugerido;
        ConfirmadoEmUtc = null;
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
