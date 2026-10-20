using System.Text.Json;
using System.Text.Json.Nodes;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerarExcelTool(IContentGenerator contentGenerator) : IFinanceTool
{
    public string Name => "gerar_excel";
    public string Description => "Gera uma planilha Excel (.xlsx) com dados financeiros organizados em abas. Use para exportar tabelas, relatórios e análises em formato editável.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "titulo": { "type": "string", "description": "Nome do arquivo" },
            "planilhas": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "nome": { "type": "string", "description": "Nome da aba" },
                  "colunas": { "type": "array", "items": { "type": "string" }, "description": "Cabeçalhos das colunas" },
                  "linhas": {
                    "type": "array",
                    "items": { "type": "array", "items": {} },
                    "description": "Linhas de dados (cada linha é um array de valores)"
                  }
                },
                "required": ["nome", "colunas", "linhas"]
              },
              "description": "Planilhas (abas) do arquivo"
            }
          },
          "required": ["titulo", "planilhas"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var titulo = input?["titulo"]?.GetValue<string>() ?? "planilha";

        var planilhasArray = input?["planilhas"]?.AsArray();
        if (planilhasArray is null || planilhasArray.Count == 0)
            return JsonSerializer.Serialize(new { erro = "Pelo menos uma planilha é obrigatória." });

        var planilhas = planilhasArray.Select(p =>
        {
            var colunas = p?["colunas"]?.AsArray().Select(c => c?.GetValue<string>() ?? "").ToList()
                ?? new List<string>();
            var linhas = p?["linhas"]?.AsArray().Select(row =>
                (IReadOnlyList<object?>)(row?.AsArray().Select(cell => (object?)cell?.ToString()).ToList()
                ?? new List<object?>())
            ).ToList() ?? new List<IReadOnlyList<object?>>();

            return new ExcelSheet(
                p?["nome"]?.GetValue<string>() ?? "Planilha",
                colunas,
                linhas);
        }).ToList();

        var request = new ExcelRequest(titulo, planilhas);
        var bytes = await contentGenerator.GerarExcelAsync(request, cancellationToken);

        if (context.FileService is null)
            return JsonSerializer.Serialize(new { erro = "Serviço de arquivos não disponível." });

        var nomeArquivo = $"{SanitizeFileName(titulo)}-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
        var fileOutput = await context.FileService.SalvarAsync(context.FamiliaId, nomeArquivo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", bytes, cancellationToken);
        context.ArquivosGerados.Add(fileOutput);

        var totalLinhas = planilhas.Sum(p => p.Linhas.Count);
        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            arquivo = new { fileOutput.Nome, fileOutput.MimeType, fileOutput.Url, fileOutput.TamanhoBytes },
            mensagem = $"Planilha '{titulo}' gerada com {planilhas.Count} aba(s) e {totalLinhas} linha(s) de dados."
        });
    }

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)))
            .Replace(' ', '-')
            .ToLowerInvariant();
}
