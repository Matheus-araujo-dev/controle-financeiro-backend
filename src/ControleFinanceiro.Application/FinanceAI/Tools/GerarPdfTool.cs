using System.Text.Json;
using System.Text.Json.Nodes;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerarPdfTool(IContentGenerator contentGenerator) : IFinanceTool
{
    public string Name => "gerar_pdf";
    public string Description => "Gera um documento PDF a partir de conteúdo textual. Use para criar relatórios, resumos e documentos financeiros formatados.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "titulo": { "type": "string", "description": "Título do documento" },
            "subtitulo": { "type": "string", "description": "Subtítulo opcional" },
            "conteudo": { "type": "string", "description": "Conteúdo do documento. Use \\n para quebras de linha, **texto** para negrito, e - para itens de lista." }
          },
          "required": ["titulo", "conteudo"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var titulo = input?["titulo"]?.GetValue<string>() ?? "documento";
        var subtitulo = input?["subtitulo"]?.GetValue<string>();
        var conteudo = input?["conteudo"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(conteudo))
            return JsonSerializer.Serialize(new { erro = "Conteúdo obrigatório para gerar o PDF." });

        var request = new PdfRequest(titulo, conteudo, subtitulo);
        var bytes = await contentGenerator.GerarPdfAsync(request, cancellationToken);

        if (context.FileService is null)
            return JsonSerializer.Serialize(new { erro = "Serviço de arquivos não disponível." });

        var nomeArquivo = $"{SanitizeFileName(titulo)}-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
        var fileOutput = await context.FileService.SalvarAsync(context.FamiliaId, nomeArquivo, "application/pdf", bytes, cancellationToken);
        context.ArquivosGerados.Add(fileOutput);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            arquivo = new { fileOutput.Nome, fileOutput.MimeType, fileOutput.Url, fileOutput.TamanhoBytes },
            mensagem = $"Documento PDF '{titulo}' gerado."
        });
    }

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)))
            .Replace(' ', '-')
            .ToLowerInvariant();
}
