using System.Text.Json;
using System.Text.Json.Nodes;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.FinanceAI.Tools;

public sealed class GerenciarInvestimentoTool(IAppDbContext db) : IFinanceTool
{
    public string Name => "gerenciar_investimento";
    public string Description => "Gerencia investimentos: registrar novo investimento, atualizar valor atual ou encerrar (resgatar). Para consultar investimentos, use buscar_investimentos.";
    public string InputSchema => """
        {
          "type": "object",
          "properties": {
            "acao":              { "type": "string",  "enum": ["criar", "atualizar_valor", "encerrar"], "description": "Ação a executar" },
            "investimentoId":    { "type": "string",  "description": "ID do investimento (obrigatório para atualizar_valor e encerrar)" },
            "nome":              { "type": "string",  "description": "Nome do investimento (criar)" },
            "emissor":           { "type": "string",  "description": "Emissor do título (criar, opcional)" },
            "tipo":              { "type": "string",  "enum": ["renda_fixa", "renda_variavel", "fundo_imobiliario", "criptomoeda", "outro"], "description": "Tipo do investimento (criar)" },
            "liquidez":          { "type": "string",  "enum": ["diaria", "vencimento", "iliquido"], "description": "Tipo de liquidez (criar)" },
            "valorInvestido":    { "type": "number",  "description": "Valor aplicado em reais (criar)" },
            "dataAplicacao":     { "type": "string",  "description": "Data da aplicação YYYY-MM-DD (criar, opcional = hoje)" },
            "dataVencimento":    { "type": "string",  "description": "Data de vencimento YYYY-MM-DD (criar, opcional)" },
            "taxaAnual":         { "type": "number",  "description": "Taxa anual em % (criar, opcional)" },
            "contaBancariaId":   { "type": "string",  "description": "ID da conta bancária vinculada (criar)" },
            "valorAtual":        { "type": "number",  "description": "Novo valor atual do investimento (atualizar_valor)" },
            "valorResgate":      { "type": "number",  "description": "Valor do resgate ao encerrar (encerrar)" }
          },
          "required": ["acao"]
        }
        """;

    public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
    {
        if (context.Papel == PapelFamilia.Visualizador)
            return JsonSerializer.Serialize(new { erro = "Visualizadores não podem gerenciar investimentos." });

        var input = JsonNode.Parse(inputJson);
        var acao = input?["acao"]?.GetValue<string>();

        try
        {
            return acao switch
            {
                "criar" => await CriarAsync(input!, cancellationToken),
                "atualizar_valor" => await AtualizarValorAsync(input!, cancellationToken),
                "encerrar" => await EncerrarAsync(input!, cancellationToken),
                _ => JsonSerializer.Serialize(new { erro = "Ação inválida. Use: criar, atualizar_valor ou encerrar." })
            };
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return JsonSerializer.Serialize(new { erro = ex.Message });
        }
    }

    private async Task<string> CriarAsync(JsonNode input, CancellationToken ct)
    {
        var nome = input["nome"]?.GetValue<string>();
        var emissor = input["emissor"]?.GetValue<string>();
        var tipoStr = input["tipo"]?.GetValue<string>();
        var liquidezStr = input["liquidez"]?.GetValue<string>();
        var valorInvestido = input["valorInvestido"]?.GetValue<decimal>() ?? 0;
        var dataAplicacaoStr = input["dataAplicacao"]?.GetValue<string>();
        var dataVencimentoStr = input["dataVencimento"]?.GetValue<string>();
        var taxaAnual = input["taxaAnual"]?.GetValue<decimal>();
        var contaBancariaIdStr = input["contaBancariaId"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(nome))
            return JsonSerializer.Serialize(new { erro = "Nome do investimento é obrigatório." });

        if (valorInvestido <= 0)
            return JsonSerializer.Serialize(new { erro = "Valor investido deve ser maior que zero." });

        if (!Guid.TryParse(contaBancariaIdStr, out var contaBancariaId))
            return JsonSerializer.Serialize(new { erro = "contaBancariaId é obrigatório e deve ser um ID válido." });

        var contaExiste = await db.ContasBancarias.AsNoTracking()
            .AnyAsync(c => c.Id == contaBancariaId, ct);
        if (!contaExiste)
            return JsonSerializer.Serialize(new { erro = "Conta bancária não encontrada." });

        var tipo = MapearTipo(tipoStr);
        if (tipo is null)
            return JsonSerializer.Serialize(new { erro = "Tipo inválido. Use: renda_fixa, renda_variavel, fundo_imobiliario, criptomoeda, outro." });

        var liquidez = MapearLiquidez(liquidezStr);
        if (liquidez is null)
            return JsonSerializer.Serialize(new { erro = "Liquidez inválida. Use: diaria, vencimento, iliquido." });

        var dataAplicacao = DateOnly.TryParse(dataAplicacaoStr, out var da) ? da : DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly? dataVencimento = DateOnly.TryParse(dataVencimentoStr, out var dv) ? dv : null;

        var investimento = Investimento.Criar(
            nome, emissor, tipo.Value, liquidez.Value,
            valorInvestido, dataAplicacao, dataVencimento,
            taxaAnual, contaBancariaId);

        db.Investimentos.Add(investimento);
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            investimentoId = investimento.Id,
            mensagem = $"Investimento '{nome}' registrado: R$ {valorInvestido:N2} aplicado em {dataAplicacao:dd/MM/yyyy}."
        });
    }

    private async Task<string> AtualizarValorAsync(JsonNode input, CancellationToken ct)
    {
        if (!Guid.TryParse(input["investimentoId"]?.GetValue<string>(), out var id))
            return JsonSerializer.Serialize(new { erro = "investimentoId é obrigatório." });

        var valorAtual = input["valorAtual"]?.GetValue<decimal>();
        if (valorAtual is null)
            return JsonSerializer.Serialize(new { erro = "valorAtual é obrigatório." });

        var investimento = await db.Investimentos
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (investimento is null)
            return JsonSerializer.Serialize(new { erro = "Investimento não encontrado." });

        investimento.AtualizarValorAtual(valorAtual.Value);
        await db.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = $"Valor atualizado para R$ {valorAtual.Value:N2}. Rendimento: R$ {investimento.Rendimento:N2} ({investimento.RendimentoPercent:N2}%)."
        });
    }

    private async Task<string> EncerrarAsync(JsonNode input, CancellationToken ct)
    {
        if (!Guid.TryParse(input["investimentoId"]?.GetValue<string>(), out var id))
            return JsonSerializer.Serialize(new { erro = "investimentoId é obrigatório." });

        var valorResgate = input["valorResgate"]?.GetValue<decimal>();
        if (valorResgate is null)
            return JsonSerializer.Serialize(new { erro = "valorResgate é obrigatório." });

        var investimento = await db.Investimentos
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (investimento is null)
            return JsonSerializer.Serialize(new { erro = "Investimento não encontrado." });

        var valorInvestido = investimento.ValorInvestido;
        investimento.Encerrar(valorResgate.Value);
        await db.SaveChangesAsync(ct);

        var rendimento = valorResgate.Value - valorInvestido;
        return JsonSerializer.Serialize(new
        {
            sucesso = true,
            mensagem = $"Investimento encerrado. Resgate: R$ {valorResgate.Value:N2}. Rendimento total: R$ {rendimento:N2}."
        });
    }

    private static TipoInvestimento? MapearTipo(string? tipo) => tipo switch
    {
        "renda_fixa" => TipoInvestimento.RendaFixa,
        "renda_variavel" => TipoInvestimento.RendaVariavel,
        "fundo_imobiliario" => TipoInvestimento.FundoImobiliario,
        "criptomoeda" => TipoInvestimento.Criptomoeda,
        "outro" => TipoInvestimento.Outro,
        _ => null
    };

    private static LiquidezInvestimento? MapearLiquidez(string? liquidez) => liquidez switch
    {
        "diaria" => LiquidezInvestimento.Diaria,
        "vencimento" => LiquidezInvestimento.Vencimento,
        "iliquido" => LiquidezInvestimento.Iliquido,
        _ => null
    };
}
