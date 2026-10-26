using System.Text.Json;
using System.Text.Json.Nodes;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerarApresentacaoTool(IContentGenerator contentGenerator) : IFinanceTool
{
    public string Name => "gerar_apresentacao";
    public string Description => "Gera uma apresentação PowerPoint (.pptx) com slides financeiros. Use para criar apresentações de resultados, análises e planejamento financeiro.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "titulo": { "type": "string", "description": "Título da apresentação" },
            "slides": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "titulo": { "type": "string", "description": "Título do slide" },
                  "conteudo": { "type": "string", "description": "Conteúdo textual do slide. Use \\n para quebras de linha, - para tópicos." },
                  "notas": { "type": "string", "description": "Notas do apresentador (opcional)" }
                },
                "required": ["titulo", "conteudo"]
              },
              "description": "Lista de slides"
            }
          },
          "required": ["titulo", "slides"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var titulo = input?["titulo"]?.GetValue<string>() ?? "apresentacao";

        var slidesArray = input?["slides"]?.AsArray();
        if (slidesArray is null || slidesArray.Count == 0)
            return JsonSerializer.Serialize(new { erro = "Pelo menos um slide é obrigatório." });

        var slides = slidesArray.Select(s => new SlideData(
            s?["titulo"]?.GetValue<string>() ?? "",
            s?["conteudo"]?.GetValue<string>() ?? "",
            s?["notas"]?.GetValue<string>()
        )).ToList();

        var request = new ApresentacaoRequest(titulo, slides);
        var bytes = await contentGenerator.GerarApresentacaoAsync(request, cancellationToken);

        if (context.FileService is null)
            return JsonSerializer.Serialize(new { erro = "Serviço de arquivos não disponível." });

        var nomeArquivo = $"{SanitizeFileName(titulo)}-{DateTime.UtcNow:yyyyMMddHHmmss}.pptx";
        var fileOutput = await context.FileService.SalvarAsync(context.FamiliaId, nomeArquivo, "application/vnd.openxmlformats-officedocument.presentationml.presentation", bytes, cancellationToken);
        context.ArquivosGerados.Add(fileOutput);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            arquivo = new { fileOutput.Nome, fileOutput.MimeType, fileOutput.Url, fileOutput.TamanhoBytes },
            mensagem = $"Apresentação '{titulo}' gerada com {slides.Count} slide(s)."
        });
    }

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)))
            .Replace(' ', '-')
            .ToLowerInvariant();
}
