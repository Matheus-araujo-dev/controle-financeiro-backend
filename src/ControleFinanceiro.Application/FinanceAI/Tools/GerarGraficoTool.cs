using System.Text.Json;
using System.Text.Json.Nodes;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerarGraficoTool(IContentGenerator contentGenerator) : IFinanceTool
{
    public string Name => "gerar_grafico";
    public string Description => "Gera um gráfico (pizza, barra, linha, area) a partir de dados financeiros e retorna como arquivo de imagem PNG. Use para visualizar distribuições, comparações e tendências.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "tipo": { "type": "string", "enum": ["pizza", "barra", "linha", "area"], "description": "Tipo do gráfico" },
            "titulo": { "type": "string", "description": "Título do gráfico" },
            "dados": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "label": { "type": "string" },
                  "valor": { "type": "number" },
                  "cor": { "type": "string", "description": "Cor hexadecimal opcional (ex: #FF5733)" }
                },
                "required": ["label", "valor"]
              },
              "description": "Pontos de dados do gráfico"
            },
            "largura": { "type": "integer", "description": "Largura em pixels (padrão 800)" },
            "altura": { "type": "integer", "description": "Altura em pixels (padrão 600)" }
          },
          "required": ["tipo", "titulo", "dados"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var tipo = input?["tipo"]?.GetValue<string>() ?? "barra";
        var titulo = input?["titulo"]?.GetValue<string>() ?? "Gráfico";
        var largura = input?["largura"]?.GetValue<int>() ?? 800;
        var altura = input?["altura"]?.GetValue<int>() ?? 600;

        var dadosArray = input?["dados"]?.AsArray();
        if (dadosArray is null || dadosArray.Count == 0)
            return JsonSerializer.Serialize(new { erro = "Dados obrigatórios para gerar o gráfico." });

        var dados = dadosArray.Select(d => new ChartDataPoint(
            d?["label"]?.GetValue<string>() ?? "",
            d?["valor"]?.GetValue<double>() ?? 0,
            d?["cor"]?.GetValue<string>()
        )).ToList();

        var request = new ChartRequest(tipo, titulo, dados, largura, altura);
        var bytes = await contentGenerator.GerarGraficoAsync(request, cancellationToken);

        if (context.FileService is null)
            return JsonSerializer.Serialize(new { erro = "Serviço de arquivos não disponível." });

        var nomeArquivo = $"grafico-{tipo}-{DateTime.UtcNow:yyyyMMddHHmmss}.png";
        var fileOutput = await context.FileService.SalvarAsync(context.FamiliaId, nomeArquivo, "image/png", bytes, cancellationToken);
        context.ArquivosGerados.Add(fileOutput);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            arquivo = new { fileOutput.Nome, fileOutput.MimeType, fileOutput.Url, fileOutput.TamanhoBytes },
            mensagem = $"Gráfico de {tipo} '{titulo}' gerado com {dados.Count} pontos de dados."
        });
    }
}
