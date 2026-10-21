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
            ?? throw new InvalidOperationException("Familia nao identificada para o anexo.");
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
            throw ValidationExceptionFactory.Create("Arquivo", "Tipo de arquivo nao permitido. Use PDF, JPG, PNG, WEBP ou TXT.");
        if (tamanhoInformado <= 0 || tamanhoInformado > MaxFileBytes)
            throw ValidationExceptionFactory.Create("Arquivo", "O arquivo deve ter conteudo e no maximo 10 MB.");

        var buffer = new MemoryStream((int)tamanhoInformado);
        await conteudo.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0 || buffer.Length > MaxFileBytes)
            throw ValidationExceptionFactory.Create("Arquivo", "O arquivo deve ter conteudo e no maximo 10 MB.");

        buffer.Position = 0;
        return buffer;
    }

    private static TipoEntidadeAnexo ParseTipoEntidade(string value) => value.Trim().ToLowerInvariant() switch
    {
        "contas-pagar" => TipoEntidadeAnexo.ContaPagar,
        "contas-receber" => TipoEntidadeAnexo.ContaReceber,
        "faturas" => TipoEntidadeAnexo.FaturaCartao,
        "compras-planejadas" => TipoEntidadeAnexo.CompraPlanejada,
        _ => throw ValidationExceptionFactory.Create("TipoEntidade", "Tipo de entidade de anexo invalido.")
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