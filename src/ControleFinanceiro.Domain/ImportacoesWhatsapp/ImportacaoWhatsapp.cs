using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.ImportacoesWhatsapp;

public sealed class ImportacaoWhatsapp : TenantEntity
{
    private readonly List<ItemImportadoWhatsapp> _itens = [];

    private ImportacaoWhatsapp()
    {
    }

    public TipoOrigemImportacaoWhatsapp TipoOrigem { get; private set; }

    public string Remetente { get; private set; } = string.Empty;

    public string? TextoBruto { get; private set; }

    public string? NomeArquivo { get; private set; }

    public string? CaminhoArquivo { get; private set; }

    public string? MimeType { get; private set; }

    public StatusImportacaoWhatsapp Status { get; private set; }

    public decimal? ConfiancaExtracao { get; private set; }

    public string? MensagemErro { get; private set; }

    public DateTime RecebidoEmUtc { get; private set; }

    public DateTime? ProcessadoEmUtc { get; private set; }

    public DateTime? ConfirmadoEmUtc { get; private set; }

    public DateTime? RejeitadoEmUtc { get; private set; }

    public IReadOnlyCollection<ItemImportadoWhatsapp> Itens => _itens;

    public static ImportacaoWhatsapp CriarRecebida(
        TipoOrigemImportacaoWhatsapp tipoOrigem,
        string remetente,
        string? textoBruto,
        string? nomeArquivo,
        string? caminhoArquivo,
        string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(remetente))
        {
            throw new ArgumentException("Remetente é obrigatório.", nameof(remetente));
        }

        return new ImportacaoWhatsapp
        {
            TipoOrigem = tipoOrigem,
            Remetente = remetente.Trim(),
            TextoBruto = string.IsNullOrWhiteSpace(textoBruto) ? null : textoBruto.Trim(),
            NomeArquivo = string.IsNullOrWhiteSpace(nomeArquivo) ? null : nomeArquivo.Trim(),
            CaminhoArquivo = string.IsNullOrWhiteSpace(caminhoArquivo) ? null : caminhoArquivo.Trim(),
            MimeType = string.IsNullOrWhiteSpace(mimeType) ? null : mimeType.Trim(),
            Status = StatusImportacaoWhatsapp.Recebido,
            RecebidoEmUtc = DateTime.UtcNow
        };
    }

    public void RegistrarArtefatoArmazenado(string caminhoArquivo)
    {
        if (string.IsNullOrWhiteSpace(caminhoArquivo))
        {
            throw new ArgumentException("Caminho do arquivo é obrigatório.", nameof(caminhoArquivo));
        }

        CaminhoArquivo = caminhoArquivo.Trim();
    }

    public void MarcarEmProcessamento()
    {
        Status = StatusImportacaoWhatsapp.EmProcessamento;
        MensagemErro = null;
        ConfirmadoEmUtc = null;
        RejeitadoEmUtc = null;
    }

    public void RegistrarExtracaoComSucesso(decimal? confiancaExtracao)
    {
        Status = StatusImportacaoWhatsapp.ExtraidoComSucesso;
        ConfiancaExtracao = confiancaExtracao;
        MensagemErro = null;
        ProcessadoEmUtc = DateTime.UtcNow;
    }

    public void RegistrarErroExtracao(string mensagemErro)
    {
        Status = StatusImportacaoWhatsapp.ErroExtracao;
        MensagemErro = string.IsNullOrWhiteSpace(mensagemErro)
            ? "Falha ao processar a importação."
            : mensagemErro.Trim();
        ProcessadoEmUtc = DateTime.UtcNow;
        ConfirmadoEmUtc = null;
        RejeitadoEmUtc = null;
    }

    public void SubstituirItens(IReadOnlyCollection<ItemImportadoWhatsapp> itens)
    {
        if (itens.Any(item => item.ImportacaoWhatsappId != Id))
        {
            throw new ArgumentException("Todos os itens devem pertencer à importação informada.", nameof(itens));
        }

        _itens.Clear();
        _itens.AddRange(itens);
        AtualizarStatusRevisao();
    }

    public void AtualizarStatusRevisao()
    {
        if (_itens.Count == 0)
        {
            Status = MensagemErro is null
                ? StatusImportacaoWhatsapp.ExtraidoComSucesso
                : StatusImportacaoWhatsapp.ErroExtracao;
            ConfirmadoEmUtc = null;
            RejeitadoEmUtc = null;
            return;
        }

        if (Status == StatusImportacaoWhatsapp.Confirmado &&
            _itens.All(item => item.Status != StatusItemImportadoWhatsapp.Sugerido))
        {
            return;
        }

        Status = StatusImportacaoWhatsapp.PendenteRevisao;
        ConfirmadoEmUtc = null;
        RejeitadoEmUtc = null;
    }

    public void AprovarRevisao()
    {
        if (_itens.Count == 0)
        {
            throw new InvalidOperationException("Importação sem itens não pode ser aprovada.");
        }

        if (_itens.Any(item => item.Status == StatusItemImportadoWhatsapp.Sugerido))
        {
            throw new InvalidOperationException("Revise todos os itens antes de aprovar a importação.");
        }

        Status = StatusImportacaoWhatsapp.Confirmado;
        ConfirmadoEmUtc ??= DateTime.UtcNow;
        RejeitadoEmUtc = null;
    }

    public void ReabrirRevisao()
    {
        if (Status != StatusImportacaoWhatsapp.Confirmado)
        {
            throw new InvalidOperationException("Somente importações aprovadas podem ser reabertas.");
        }

        foreach (var item in _itens)
        {
            item.HabilitarEdicaoAposReabertura();
        }

        Status = StatusImportacaoWhatsapp.PendenteRevisao;
        ConfirmadoEmUtc = null;
        RejeitadoEmUtc = null;
    }
}
