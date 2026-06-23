using System.Security.Cryptography;
using System.Text;
using ControleFinanceiro.Infrastructure.Anexos;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace ControleFinanceiro.Infrastructure.Tests.Anexos;

public sealed class LocalAnexoFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"cf-anexos-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveEOpenReadAsync_DevePersistirConteudoComHashEArquivoSanitizado()
    {
        var storage = CriarStorage();
        var content = Encoding.UTF8.GetBytes("conteudo do comprovante");
        await using var input = new MemoryStream(content);

        var result = await storage.SaveAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "comprovante:pix?.pdf",
            input,
            CancellationToken.None);

        result.TamanhoBytes.Should().Be(content.Length);
        result.HashSha256.Should().Be(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant());
        result.CaminhoArquivo.Should().NotContain(":").And.NotContain("?");

        await using var stored = await storage.OpenReadAsync(result.CaminhoArquivo, CancellationToken.None);
        using var reader = new StreamReader(stored, Encoding.UTF8);
        (await reader.ReadToEndAsync()).Should().Be("conteudo do comprovante");
    }

    [Fact]
    public async Task DeleteAsync_DeveRemoverArquivoEDiretorioDoAnexo()
    {
        var storage = CriarStorage();
        await using var input = new MemoryStream([1, 2, 3]);
        var result = await storage.SaveAsync(Guid.NewGuid(), Guid.NewGuid(), "foto.jpg", input, CancellationToken.None);

        await storage.DeleteAsync(result.CaminhoArquivo, CancellationToken.None);

        var action = () => storage.OpenReadAsync(result.CaminhoArquivo, CancellationToken.None);
        await action.Should().ThrowAsync<FileNotFoundException>();
    }

    private LocalAnexoFileStorage CriarStorage() => new(new TestEnvironment(_root));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class TestEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
