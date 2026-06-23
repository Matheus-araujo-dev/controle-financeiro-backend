using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Anexos;

public sealed class Anexo : TenantEntity
{
    private readonly List<AnexoVinculo> _vinculos = [];

    private Anexo() { }

    public string NomeArquivoOriginal { get; private set; } = string.Empty;
    public string CaminhoArquivo { get; private set; } = string.Empty;
    public string MimeType { get; private set; } = string.Empty;
    public long TamanhoBytes { get; private set; }
    public string HashSha256 { get; private set; } = string.Empty;
    public OrigemAnexo Origem { get; private set; }
    public Guid? ConversaAiId { get; private set; }
    public Guid? ImportacaoWhatsappId { get; private set; }
    public IReadOnlyCollection<AnexoVinculo> Vinculos => _vinculos;

    public static Anexo Criar(
        string nomeArquivoOriginal,
        string caminhoArquivo,
        string mimeType,
        long tamanhoBytes,
        string hashSha256,
        OrigemAnexo origem,
        Guid? conversaAiId,
        Guid? importacaoWhatsappId)
    {
        if (string.IsNullOrWhiteSpace(nomeArquivoOriginal)) throw new ArgumentException("Nome do arquivo é obrigatório.", nameof(nomeArquivoOriginal));
        if (string.IsNullOrWhiteSpace(caminhoArquivo)) throw new ArgumentException("Caminho do arquivo é obrigatório.", nameof(caminhoArquivo));
        if (string.IsNullOrWhiteSpace(mimeType)) throw new ArgumentException("Tipo do arquivo é obrigatório.", nameof(mimeType));
        if (tamanhoBytes <= 0) throw new ArgumentOutOfRangeException(nameof(tamanhoBytes), "Arquivo vazio não é permitido.");
        if (hashSha256.Length != 64) throw new ArgumentException("Hash SHA-256 inválido.", nameof(hashSha256));

        return new Anexo
        {
            NomeArquivoOriginal = nomeArquivoOriginal.Trim(),
            CaminhoArquivo = caminhoArquivo.Trim(),
            MimeType = mimeType.Trim().ToLowerInvariant(),
            TamanhoBytes = tamanhoBytes,
            HashSha256 = hashSha256.Trim().ToLowerInvariant(),
            Origem = origem,
            ConversaAiId = conversaAiId,
            ImportacaoWhatsappId = importacaoWhatsappId
        };
    }

    public void Vincular(TipoEntidadeAnexo tipoEntidade, Guid entidadeId)
    {
        if (entidadeId == Guid.Empty) throw new ArgumentException("Entidade vinculada é obrigatória.", nameof(entidadeId));
        if (_vinculos.Any(x => x.TipoEntidade == tipoEntidade && x.EntidadeId == entidadeId)) return;

        _vinculos.Add(AnexoVinculo.Criar(Id, tipoEntidade, entidadeId));
    }
}
