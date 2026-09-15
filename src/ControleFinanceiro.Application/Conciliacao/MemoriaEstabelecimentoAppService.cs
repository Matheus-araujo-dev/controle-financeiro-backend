using System.Text.Json;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Conciliacao;
using ControleFinanceiro.Domain.Conciliacao;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Conciliacao;

public sealed class MemoriaEstabelecimentoAppService(IAppDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyDictionary<string, PreferenciasFaturaResponse>> ConsultarAsync(Guid cartaoId, IEnumerable<string> descricoes, CancellationToken ct)
    {
        var keys = descricoes.Select(EstabelecimentoKey.Normalizar).Distinct().ToArray();
        var memorias = await db.MemoriasEstabelecimento.AsNoTracking().Where(x => x.CartaoId == cartaoId && keys.Contains(x.Chave)).ToListAsync(ct);
        return memorias.ToDictionary(x => x.Chave, x => JsonSerializer.Deserialize<PreferenciasFaturaResponse>(x.PreferenciasJson, JsonOptions)!);
    }

    // Chamado apenas na confirmação; não grava sozinho, participando da mesma transação da conta.
    public async Task AprenderAsync(ItemConciliacao item, ContaPagar conta, bool aprender, IReadOnlyList<string>? campos, CancellationToken ct)
    {
        if (!aprender || campos is { Count: 0 } || !conta.CartaoId.HasValue) return;
        var key = EstabelecimentoKey.Normalizar(item.Descricao);
        if (key.Length == 0) return;
        var rateios = conta.Rateios.Count > 0 ? conta.Rateios.ToList()
            : await db.RateiosContaGerencial.AsNoTracking().Where(x => x.ContaPagarId == conta.Id).ToListAsync(ct);
        var pagadores = conta.GrupoReembolsoId.HasValue
            ? await db.ContasReceber.AsNoTracking().Where(x => x.GrupoReembolsoId == conta.GrupoReembolsoId && x.StatusContaId != StatusConta.CanceladaId)
                .Select(x => x.PagadorId).Distinct().ToArrayAsync(ct) : [];
        var valores = new Dictionary<string, string>
        {
            ["descricao"] = JsonSerializer.Serialize(conta.Descricao, JsonOptions),
            ["responsavelCompraId"] = JsonSerializer.Serialize(conta.ResponsavelCompraId, JsonOptions),
            ["recebedorId"] = JsonSerializer.Serialize(conta.RecebedorId, JsonOptions),
            ["rateios"] = JsonSerializer.Serialize(rateios.Select(r => new RateioMemoriaResponse(r.ContaGerencialId, r.Valor / conta.ValorLiquido)), JsonOptions),
            ["ehRecorrente"] = JsonSerializer.Serialize(conta.EhRecorrente, JsonOptions),
            ["gerarReembolso"] = JsonSerializer.Serialize(conta.GrupoReembolsoId.HasValue, JsonOptions),
            ["reembolsoPagadoresIds"] = JsonSerializer.Serialize(pagadores, JsonOptions)
        };
        if (campos is not null)
        {
            if (campos.Any(x => !valores.ContainsKey(x))) throw ValidationExceptionFactory.Create("CamposParaAprender", "Campo de memória inválido.");
            valores = valores.Where(x => campos.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
        }
        var memoria = await db.MemoriasEstabelecimento.SingleOrDefaultAsync(x => x.CartaoId == conta.CartaoId && x.Chave == key, ct);
        if (memoria is null) { memoria = MemoriaEstabelecimento.Criar(conta.CartaoId.Value, key); db.MemoriasEstabelecimento.Add(memoria); }
        memoria.AplicarDecisao(valores);
    }
}
