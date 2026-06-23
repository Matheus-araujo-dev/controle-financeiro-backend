using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Anexos;
using ControleFinanceiro.Domain.Anexos;
using ControleFinanceiro.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Anexos;

public sealed class AnexoAppService(
    IAppDbContext dbContext,
    IAnexoFileStorage fileStorage,
    ICurrentUser currentUser)
{
    private const int MaxFileBytes = 10 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string[]> AllowedExtensions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = [".pdf"],
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["image/png"] = [".png"],
            ["image/webp"] = [".webp"],
            ["text/plain"] = [".txt"]
        };

    public async Task<IReadOnlyCollection<AnexoResponse>?> ListarAsync(
        string tipoEntidade,
        Guid entidadeId,
        CancellationToken cancellationToken)
    {
        var tipo = ParseTipoEntidade(tipoEntidade);
        if (!await EntidadeExisteAsync(tipo, entidadeId, cancellationToken)) return null;

        return await (
                from vinculo in dbContext.AnexoVinculos.AsNoTracking()
                join anexo in dbContext.Anexos.AsNoTracking() on vinculo.AnexoId equals anexo.Id
                where vinculo.TipoEntidade == tipo && vinculo.EntidadeId == entidadeId
                orderby anexo.CreatedAtUtc descending
                select Map(anexo))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<AnexoResponse?> AdicionarAsync(
        string tipoEntidade,
        Guid entidadeId,
        string nomeArquivo,
        string mimeType,
        long tamanhoInformado,
        Stream conteudo,
        CancellationToken cancellationToken)
    {
        var tipo = ParseTipoEntidade(tipoEntidade);
        if (!await EntidadeExisteAsync(tipo, entidadeId, cancellationToken)) return null;

        var familiaId = currentUser.FamiliaId
            ?? throw new InvalidOperationException("Família não identificada para o anexo.");
        var buffer = await LerEValidarAsync(nomeArquivo, mimeType, tamanhoInformado, conteudo, cancellationToken);
        var storage = await fileStorage.SaveAsync(
            familiaId,
            Guid.NewGuid(),
            nomeArquivo,
            buffer,
            cancellationToken);

        var anexo = Anexo.Criar(
            nomeArquivo,
            storage.CaminhoArquivo,
            mimeType,
            storage.TamanhoBytes,
            storage.HashSha256,
            OrigemAnexo.Manual,
            null,
            null);
        anexo.Vincular(tipo, entidadeId);
        dbContext.Anexos.Add(anexo);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(anexo);
    }

    public async Task<AnexoConteudoResult?> ObterConteudoAsync(Guid anexoId, CancellationToken cancellationToken)
    {
        var anexo = await dbContext.Anexos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == anexoId, cancellationToken);
        if (anexo is null) return null;

        var stream = await fileStorage.OpenReadAsync(anexo.CaminhoArquivo, cancellationToken);
        return new AnexoConteudoResult(stream, anexo.MimeType, anexo.NomeArquivoOriginal);
    }

    public async Task<bool> ExcluirAsync(
        string tipoEntidade,
        Guid entidadeId,
        Guid anexoId,
        CancellationToken cancellationToken)
    {
        var tipo = ParseTipoEntidade(tipoEntidade);
        var anexo = await dbContext.Anexos
            .Include(x => x.Vinculos)
            .SingleOrDefaultAsync(x => x.Id == anexoId, cancellationToken);
        if (anexo is null) return false;

        var vinculo = anexo.Vinculos.SingleOrDefault(x => x.TipoEntidade == tipo && x.EntidadeId == entidadeId);
        if (vinculo is null) return false;

        dbContext.AnexoVinculos.Remove(vinculo);
        var removerArquivo = anexo.Vinculos.Count == 1;
        if (removerArquivo) dbContext.Anexos.Remove(anexo);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (removerArquivo) await fileStorage.DeleteAsync(anexo.CaminhoArquivo, cancellationToken);
        return true;
    }

    public async Task<Anexo> CriarPendenteAsync(
        Guid familiaId,
        Guid conversaAiId,
        string nomeArquivo,
        string mimeType,
        string arquivoBase64,
        CancellationToken cancellationToken)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(arquivoBase64); }
        catch (FormatException exception)
        {
            throw ValidationExceptionFactory.Create("MidiaBase64", $"Arquivo base64 inválido: {exception.Message}");
        }

        await using var input = new MemoryStream(bytes, writable: false);
        var validado = await LerEValidarAsync(nomeArquivo, mimeType, bytes.Length, input, cancellationToken);
        var storage = await fileStorage.SaveAsync(familiaId, Guid.NewGuid(), nomeArquivo, validado, cancellationToken);
        var anexo = Anexo.Criar(
            nomeArquivo,
            storage.CaminhoArquivo,
            mimeType,
            storage.TamanhoBytes,
            storage.HashSha256,
            OrigemAnexo.Whatsapp,
            conversaAiId,
            null);
        anexo.AtribuirFamilia(familiaId);
        dbContext.Anexos.Add(anexo);
        return anexo;
    }

    public async Task<int> VincularPendentesDaConversaAsync(
        Guid conversaAiId,
        TipoEntidadeAnexo tipoEntidade,
        IReadOnlyCollection<Guid> entidadeIds,
        CancellationToken cancellationToken)
    {
        var anexoIds = await dbContext.Anexos
            .AsNoTracking()
            .Where(x => x.ConversaAiId == conversaAiId &&
                        !dbContext.AnexoVinculos.Any(vinculo => vinculo.AnexoId == x.Id))
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);

        foreach (var anexoId in anexoIds)
        foreach (var entidadeId in entidadeIds.Distinct())
            dbContext.AnexoVinculos.Add(AnexoVinculo.Criar(anexoId, tipoEntidade, entidadeId));

        return anexoIds.Length;
    }

    private async Task<bool> EntidadeExisteAsync(TipoEntidadeAnexo tipo, Guid id, CancellationToken cancellationToken)
    {
        return tipo switch
        {
            TipoEntidadeAnexo.ContaPagar => await dbContext.ContasPagar.AnyAsync(x => x.Id == id, cancellationToken),
            TipoEntidadeAnexo.ContaReceber => await dbContext.ContasReceber.AnyAsync(x => x.Id == id, cancellationToken),
            TipoEntidadeAnexo.FaturaCartao => await dbContext.FaturasCartao.AnyAsync(x => x.Id == id, cancellationToken),
            TipoEntidadeAnexo.CompraPlanejada => await dbContext.ComprasPlanejadas.AnyAsync(x => x.Id == id, cancellationToken),
            _ => false
        };
    }

    private static async Task<MemoryStream> LerEValidarAsync(
        string nomeArquivo,
        string mimeType,
        long tamanhoInformado,
        Stream conteudo,
        CancellationToken cancellationToken)
    {
        var normalizedMime = mimeType.Trim().ToLowerInvariant();
        var extension = Path.GetExtension(nomeArquivo).ToLowerInvariant();
        if (!AllowedExtensions.TryGetValue(normalizedMime, out var extensions) || !extensions.Contains(extension))
            throw ValidationExceptionFactory.Create("Arquivo", "Tipo de arquivo não permitido. Use PDF, JPG, PNG, WEBP ou TXT.");
        if (tamanhoInformado <= 0 || tamanhoInformado > MaxFileBytes)
            throw ValidationExceptionFactory.Create("Arquivo", "O arquivo deve ter conteúdo e no máximo 10 MB.");

        var buffer = new MemoryStream((int)tamanhoInformado);
        await conteudo.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0 || buffer.Length > MaxFileBytes)
            throw ValidationExceptionFactory.Create("Arquivo", "O arquivo deve ter conteúdo e no máximo 10 MB.");
        if (!AssinaturaValida(normalizedMime, buffer.GetBuffer().AsSpan(0, (int)buffer.Length)))
            throw ValidationExceptionFactory.Create("Arquivo", "O conteúdo do arquivo não corresponde ao tipo informado.");

        buffer.Position = 0;
        return buffer;
    }

    private static bool AssinaturaValida(string mimeType, ReadOnlySpan<byte> bytes) => mimeType switch
    {
        "application/pdf" => bytes.StartsWith("%PDF-"u8),
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
        "image/png" => bytes.Length >= 8 &&
                       bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
        "image/webp" => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
        "text/plain" => true,
        _ => false
    };

    private static TipoEntidadeAnexo ParseTipoEntidade(string value) => value.Trim().ToLowerInvariant() switch
    {
        "contas-pagar" => TipoEntidadeAnexo.ContaPagar,
        "contas-receber" => TipoEntidadeAnexo.ContaReceber,
        "faturas" => TipoEntidadeAnexo.FaturaCartao,
        "compras-planejadas" => TipoEntidadeAnexo.CompraPlanejada,
        _ => throw ValidationExceptionFactory.Create("TipoEntidade", "Tipo de entidade de anexo inválido.")
    };

    private static AnexoResponse Map(Anexo anexo) => new(
        anexo.Id,
        anexo.NomeArquivoOriginal,
        anexo.MimeType,
        anexo.TamanhoBytes,
        anexo.Origem.ToString(),
        anexo.CreatedAtUtc,
        $"/api/v1/anexos/{anexo.Id}/conteudo");
}

public sealed record AnexoConteudoResult(Stream Conteudo, string MimeType, string NomeArquivo);
