using System.Text;
using ControleFinanceiro.Application.ImportacoesWhatsapp;
using ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace ControleFinanceiro.Infrastructure.Tests.ImportacoesWhatsapp;

public sealed class LocalImportFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cf-storage-tests", Guid.NewGuid().ToString("N"));

    private LocalImportFileStorage CriarStorage() =>
        new(new FakeWebHostEnvironment { ContentRootPath = _root });

    [Fact]
    public async Task SaveAsync_ComBase64Valido_DeveGravarArquivoERetornarCaminhoRelativo()
    {
        var conteudo = Encoding.UTF8.GetBytes("conteudo do arquivo");
        var importacaoId = Guid.NewGuid();
        var request = new FileStorageRequest(importacaoId, "nota.pdf", "application/pdf", Convert.ToBase64String(conteudo));

        var resultado = await CriarStorage().SaveAsync(request, CancellationToken.None);

        resultado.CaminhoArquivo.Should().Be($"App_Data/importacoes-whatsapp/{importacaoId:N}/nota.pdf");
        resultado.CaminhoArquivo.Should().NotContain("\\");
        var absolute = Path.Combine(_root, resultado.CaminhoArquivo.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(absolute).Should().BeTrue();
        (await File.ReadAllBytesAsync(absolute)).Should().Equal(conteudo);
    }

    [Fact]
    public async Task SaveAsync_ComNomeContendoCaracteresInvalidos_DeveSanitizar()
    {
        var request = new FileStorageRequest(
            Guid.NewGuid(), "re/la:tó*rio.pdf", "application/pdf", Convert.ToBase64String([1, 2, 3]));

        var resultado = await CriarStorage().SaveAsync(request, CancellationToken.None);

        resultado.CaminhoArquivo.Should().EndWith("re_la_tó_rio.pdf");
    }

    [Fact]
    public async Task SaveAsync_ComBase64Invalido_DeveLancarArgumentException()
    {
        var request = new FileStorageRequest(Guid.NewGuid(), "x.pdf", "application/pdf", "@@@nao-e-base64@@@");

        var acao = async () => await CriarStorage().SaveAsync(request, CancellationToken.None);

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveAsync_ComArquivoAcimaDoLimite_DeveLancarArgumentException()
    {
        var grande = new byte[(10 * 1024 * 1024) + 1];
        var request = new FileStorageRequest(Guid.NewGuid(), "grande.pdf", "application/pdf", Convert.ToBase64String(grande));

        var acao = async () => await CriarStorage().SaveAsync(request, CancellationToken.None);

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ControleFinanceiro.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
