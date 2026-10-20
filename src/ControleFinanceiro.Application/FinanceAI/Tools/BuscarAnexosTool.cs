using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Anexos;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarAnexosTool(AnexoAppService service) : IFinanceTool
{
    public string Name => "buscar_anexos";
    public string Description => "Lista anexos vinculados a uma entidade financeira (conta a pagar, conta a receber, fatura ou compra planejada). Retorna nome, tipo, tamanho e URL de cada anexo.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "tipoEntidade": { "type": "string", "enum": ["contas-pagar", "contas-receber", "faturas", "compras-planejadas"], "description": "Tipo da entidade que contém os anexos" },
            "entidadeId":   { "type": "string", "description": "ID da entidade (GUID)" }
          },
          "required": ["tipoEntidade", "entidadeId"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var tipoEntidade = input?["tipoEntidade"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(tipoEntidade))
            return JsonSerializer.Serialize(new { erro = "tipoEntidade é obrigatório." });

        if (!Guid.TryParse(input?["entidadeId"]?.GetValue<string>(), out var entidadeId))
            return JsonSerializer.Serialize(new { erro = "entidadeId é obrigatório e deve ser um GUID válido." });

        try
        {
            var anexos = await service.ListarAsync(tipoEntidade, entidadeId, cancellationToken);

            if (anexos is null)
                return JsonSerializer.Serialize(new { erro = "Entidade não encontrada." });

            return JsonSerializer.Serialize(new
            {
                tipoEntidade,
                entidadeId,
                totalAnexos = anexos.Count,
                anexos = anexos.Select(a => new
                {
                    id = a.Id,
                    nomeArquivo = a.NomeArquivo,
                    mimeType = a.MimeType,
                    tamanhoBytes = a.TamanhoBytes,
                    origem = a.Origem,
                    criadoEm = a.CriadoEmUtc.ToString("yyyy-MM-dd HH:mm"),
                    urlConteudo = a.UrlConteudo
                })
            });
        }
        catch (ArgumentException ex)
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
    }
}
