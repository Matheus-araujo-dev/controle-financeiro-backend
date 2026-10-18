using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class BuscarInvestimentosTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "buscar_investimentos";
    public string Description => "Lista os investimentos da família. Pode filtrar por ativos ou por tipo (rendaFixa, rendaVariavel, fundoImobiliario, criptomoeda, outro).";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "apenasAtivos": { "type": "boolean", "description": "Se true, retorna apenas investimentos ativos (padrão true)" },
            "tipo":         { "type": "string", "enum": ["renda_fixa","renda_variavel","fundo_imobiliario","criptomoeda","outro"], "description": "Filtrar por tipo (opcional)" }
          }
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        var input = JsonNode.Parse(inputJson);
        var apenasAtivos = input?["apenasAtivos"]?.GetValue<bool>() ?? true;
        var tipoStr = input?["tipo"]?.GetValue<string>();

        var query = db.Investimentos.AsNoTracking().AsQueryable();

        if (apenasAtivos)
            query = query.Where(i => !i.Encerrado);

        if (TryParseTipo(tipoStr, out var tipoEnum))
            query = query.Where(i => i.Tipo == tipoEnum);

        var contasDict = await db.ContasBancarias
            .AsNoTracking()
            .Select(c => new { c.Id, c.Nome })
            .ToDictionaryAsync(c => c.Id, c => c.Nome, cancellationToken);

        var investimentos = await query
            .OrderByDescending(i => i.ValorAtual)
            .ToListAsync(cancellationToken);

        var resultado = investimentos.Select(i => new
        {
            id = i.Id,
            nome = i.Nome,
            emissor = i.Emissor,
            tipo = FormatTipo(i.Tipo),
            liquidez = FormatLiquidez(i.Liquidez),
            valorInvestido = i.ValorInvestido,
            valorAtual = i.ValorAtual,
            rendimento = i.Rendimento,
            rendimentoPercent = decimal.Round(i.RendimentoPercent, 2),
            dataAplicacao = i.DataAplicacao.ToString("dd/MM/yyyy"),
            dataVencimento = i.DataVencimento?.ToString("dd/MM/yyyy"),
            taxaAnual = i.TaxaAnual,
            contaBancaria = contasDict.GetValueOrDefault(i.ContaBancariaVinculadaId, "Desconhecida"),
            encerrado = i.Encerrado
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            total = resultado.Count,
            totalInvestido = investimentos.Sum(i => i.ValorInvestido),
            totalAtual = investimentos.Sum(i => i.ValorAtual),
            rendimentoTotal = investimentos.Sum(i => i.Rendimento),
            investimentos = resultado
        });
    }

    private static bool TryParseTipo(string? tipo, out TipoInvestimento result)
    {
        result = tipo switch
        {
            "renda_fixa" => TipoInvestimento.RendaFixa,
            "renda_variavel" => TipoInvestimento.RendaVariavel,
            "fundo_imobiliario" => TipoInvestimento.FundoImobiliario,
            "criptomoeda" => TipoInvestimento.Criptomoeda,
            "outro" => TipoInvestimento.Outro,
            _ => default
        };
        return tipo is not null && result != default;
    }

    private static string FormatTipo(TipoInvestimento tipo) => tipo switch
    {
        TipoInvestimento.RendaFixa => "Renda Fixa",
        TipoInvestimento.RendaVariavel => "Renda Variável",
        TipoInvestimento.FundoImobiliario => "Fundo Imobiliário",
        TipoInvestimento.Criptomoeda => "Criptomoeda",
        TipoInvestimento.Outro => "Outro",
        _ => tipo.ToString()
    };

    private static string FormatLiquidez(LiquidezInvestimento liquidez) => liquidez switch
    {
        LiquidezInvestimento.Diaria => "Diária",
        LiquidezInvestimento.Vencimento => "No Vencimento",
        LiquidezInvestimento.Iliquido => "Ilíquido",
        _ => liquidez.ToString()
    };
}
