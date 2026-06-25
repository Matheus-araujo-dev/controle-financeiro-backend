using System.Text.Json.Nodes;
using ControleFinanceiro.Application.FinanceAI;
using Microsoft.Extensions.Logging;

namespace ControleFinanceiro.Infrastructure.FinanceAI;

public sealed class ClaudeVisionExtracaoService(
    ILlmVisionClient vision,
    ILogger<ClaudeVisionExtracaoService> logger) : IExtracaoImagemFinanceiroService
{
    private const string SystemPrompt = """
        Você é um extrator de dados financeiros de comprovantes, notas fiscais e recibos.
        Analise a imagem e extraia os dados financeiros presentes.
        Responda APENAS com JSON válido (sem markdown, sem texto extra):
        {"sucesso": true, "estabelecimento": "Nome do local", "valor": 99.90, "data": "2024-01-15", "descricao": "Descrição breve"}
        Regras:
        - "data" no formato YYYY-MM-DD; se não aparecer, omita o campo
        - "valor" como número decimal com ponto (ex: 49.90); use o total da nota
        - "estabelecimento" é o nome da loja, restaurante ou prestador
        - "descricao" é um resumo curto do que foi comprado/pago
        Se não conseguir extrair dados confiáveis: {"sucesso": false}
        """;

    private const string PaymentFieldsPrompt = """

        Inclua tambem, quando identificavel:
        - "meioPagamento": Pix, CartaoCredito, CartaoDebito, Dinheiro, Boleto ou Outro
        - "quantidadeParcelas": inteiro positivo; use 1 para pagamento unico
        - "finalCartao": quatro ultimos digitos visiveis
        - "bandeiraCartao": bandeira inferida por texto ou logotipo
        Use texto e logotipos para identificar o estabelecimento, sem inventar dados ausentes.
        """;

    public async Task<ExtracaoImagemResultado> ExtrairAsync(
        string imagemBase64, string mimeType, CancellationToken cancellationToken)
    {
        try
        {
            var raw = await vision.AnalisarImagemAsync(
                SystemPrompt + PaymentFieldsPrompt,
                "Extraia os dados financeiros desta imagem de comprovante ou nota fiscal.",
                imagemBase64,
                mimeType,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(raw))
                return Falhou();

            var json = LimparJson(raw);
            var node = JsonNode.Parse(json);

            if (node?["sucesso"]?.GetValue<bool>() != true)
                return Falhou();

            DateOnly? data = null;
            var dataStr = node["data"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(dataStr) && DateOnly.TryParseExact(dataStr, "yyyy-MM-dd", out var d))
                data = d;

            decimal? valor = null;
            var valorNode = node["valor"];
            if (valorNode != null)
            {
                try { valor = (decimal)valorNode.GetValue<double>(); }
                catch { /* ignora valor malformado */ }
            }

            int? quantidadeParcelas = null;
            var parcelasNode = node["quantidadeParcelas"];
            if (parcelasNode != null)
            {
                try
                {
                    var parsed = parcelasNode.GetValue<int>();
                    quantidadeParcelas = parsed > 0 ? parsed : null;
                }
                catch { }
            }

            return new ExtracaoImagemResultado(
                Sucesso: true,
                Estabelecimento: node["estabelecimento"]?.GetValue<string>(),
                Valor: valor,
                Data: data,
                Descricao: node["descricao"]?.GetValue<string>(),
                MeioPagamento: node["meioPagamento"]?.GetValue<string>(),
                QuantidadeParcelas: quantidadeParcelas,
                FinalCartao: node["finalCartao"]?.GetValue<string>(),
                BandeiraCartao: node["bandeiraCartao"]?.GetValue<string>());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao extrair dados financeiros da imagem.");
            return Falhou();
        }
    }

    private static ExtracaoImagemResultado Falhou() => new(false, null, null, null, null);

    private static string LimparJson(string raw)
    {
        var s = raw.Trim();
        if (!s.StartsWith("{"))
        {
            var start = s.IndexOf('{');
            var end = s.LastIndexOf('}');
            if (start >= 0 && end > start)
                return s[start..(end + 1)];
        }
        return s;
    }
}
