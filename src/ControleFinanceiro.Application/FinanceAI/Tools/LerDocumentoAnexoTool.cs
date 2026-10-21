using System.Text;
using System.Text.Json;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class LerDocumentoAnexoTool(IDocumentReaderService documentReader) : IFinanceTool
{
    public string Name => "ler_documento_anexo";
    public string Description => "Lê o conteúdo de um arquivo anexado pelo usuário (PDF, Excel, CSV, OFX, texto). Use quando o usuário enviar um documento e você precisar ler/analisar o conteúdo.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "formato": { "type": "string", "description": "Formato esperado: auto (detectar), pdf, excel, csv, ofx, texto. Padrão: auto" }
          }
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(context.ArquivoAnexoPath) || !File.Exists(context.ArquivoAnexoPath))
            return JsonSerializer.Serialize(new { erro = "Nenhum arquivo anexado na mensagem atual. Peça ao usuário para enviar o documento." });

        var result = await documentReader.LerAsync(context.ArquivoAnexoPath, context.ArquivoAnexoNome, cancellationToken);

        if (!result.Sucesso)
            return JsonSerializer.Serialize(new { erro = result.MensagemErro ?? "Não foi possível ler o documento." });

        const int maxChars = 15_000;
        var conteudo = result.Conteudo!.Length > maxChars
            ? result.Conteudo[..maxChars] + "\n\n[... conteúdo truncado, documento muito grande ...]"
            : result.Conteudo;

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            nomeArquivo = context.ArquivoAnexoNome ?? Path.GetFileName(context.ArquivoAnexoPath),
            formato = result.FormatoDetectado,
            totalLinhas = result.TotalLinhas,
            conteudo
        });
    }
}
