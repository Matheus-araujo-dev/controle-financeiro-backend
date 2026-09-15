using System.Text.Json;
using ControleFinanceiro.Application.Financeiro.Reembolsos;
using ControleFinanceiro.Contracts.Financeiro.Reembolsos;
using System.Security.Cryptography;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Application.Financeiro.ContasPagar;
using ControleFinanceiro.Application.Financeiro.Importacao;
using ControleFinanceiro.Contracts.Conciliacao;
using ControleFinanceiro.Domain.Conciliacao;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Conciliacao;

public sealed class ConciliacaoFaturaAppService(IAppDbContext db, IPdfFaturaReader reader, ContaPagarSharedHelper helper, MemoriaEstabelecimentoAppService memoria, IAtomicOperation atomic, IReembolsoAppService reembolsos)
{
    public async Task<ConciliacaoFaturaResponse?> IniciarAsync(Guid faturaId, string nome, Stream arquivo, CancellationToken ct)
    {
        var fatura = await db.FaturasCartao.AsNoTracking().SingleOrDefaultAsync(x => x.Id == faturaId, ct);
        if (fatura is null) return null;
        if (!string.Equals(Path.GetExtension(nome), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw Erro("Envie o PDF da fatura.");
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await arquivo.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > 5 * 1024 * 1024) throw Erro("O PDF deve ter até 5 MB.");
            buffer.Write(chunk, 0, read);
        }
        if (buffer.Length == 0) throw Erro("Arquivo vazio.");
        var hash = Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
        var existente = await db.Conciliacoes.AsNoTracking().SingleOrDefaultAsync(x => x.FaturaId == faturaId && x.HashArquivo == hash, ct);
        if (existente is not null) return await ObterAsync(faturaId, existente.Id, ct);
        buffer.Position = 0;
        var parsed = await reader.ParseAsync(buffer, ct);
        if (parsed.Itens.Count == 0) throw Erro(parsed.AvisoFormato ?? "Nenhum lançamento encontrado.");
        if (parsed.Itens.Any(x => x.DataVencimentoFatura != fatura.DataVencimento))
            throw Erro("O vencimento do PDF não corresponde à fatura selecionada.");
        var ocorrencias = new Dictionary<string, int>();
        var itens = parsed.Itens.Select(x =>
        {
            var key = CsvFaturaParser.GerarChave(x);
            ocorrencias.TryGetValue(key, out var count);
            ocorrencias[key] = ++count;
            if (count > 1) key += $"-{count}";
            return ItemConciliacao.CriarFatura(x.DataTransacao, x.Descricao, x.Valor, key, x.NumeroParcela, x.QuantidadeParcelas);
        }).ToArray();
        var session = Domain.Conciliacao.Conciliacao.CriarFatura(Path.GetFileName(nome), faturaId, hash, itens);
        db.Conciliacoes.Add(session);
        await db.SaveChangesAsync(ct);
        return await ObterAsync(faturaId, session.Id, ct);
    }

    public async Task<ConciliacaoFaturaResponse?> ObterAsync(Guid faturaId, Guid id, CancellationToken ct)
    {
        var session = await db.Conciliacoes.AsNoTracking().Include(x => x.Itens)
            .SingleOrDefaultAsync(x => x.Id == id && x.FaturaId == faturaId, ct);
        if (session is null) return null;
        var contas = await db.ContasPagar.AsNoTracking()
            .Where(x => x.FaturaCartaoId == faturaId && x.StatusContaId != StatusConta.CanceladaId).ToListAsync(ct);
        var contaIds = contas.Select(x => x.Id).ToArray();
        var rateios = await db.RateiosContaGerencial.AsNoTracking().Where(x => x.ContaPagarId.HasValue && contaIds.Contains(x.ContaPagarId.Value)).ToListAsync(ct);
        var consumidas = session.Itens.Where(x => x.ContaPagarVinculadaId.HasValue).Select(x => x.ContaPagarVinculadaId!.Value).ToHashSet();
        var comparaveis = contas.Where(x => !consumidas.Contains(x.Id)).Select(Comparavel).ToArray();
        var cartaoId = await db.FaturasCartao.Where(x => x.Id == faturaId).Select(x => x.CartaoId).SingleAsync(ct);
        var preferencias = await memoria.ConsultarAsync(cartaoId, session.Itens.Select(x => x.Descricao), ct);
        var sugestoes = session.Itens.Where(x => x.StatusItem == StatusItemConciliacao.Pendente)
            .ToDictionary(x => x.Id, x => FaturaMatching.Encontrar(new(x.Id, x.Data, x.Descricao, x.Valor, x.NumeroParcela, x.QuantidadeParcelas), comparaveis, preferencias.GetValueOrDefault(EstabelecimentoKey.Normalizar(x.Descricao))?.Descricao));
        var disputadas = sugestoes.Values.SelectMany(x => x.Where(c => c.CorrespondenciaClara))
            .GroupBy(x => x.ContaId).Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet();
        return new(session.Id, faturaId, session.NomeArquivo, session.Status.ToString(),
            session.Itens.OrderBy(x => x.Data).ThenBy(x => x.Id).Select(x => new ItemFaturaConciliacaoResponse(
                x.Id, x.Data, x.Descricao, x.Valor, x.NumeroParcela, x.QuantidadeParcelas, x.StatusItem.ToString(),
                x.ContaPagarVinculadaId, x.ValorAnteriorSistema,
                x.StatusItem == StatusItemConciliacao.Pendente
                    ? sugestoes[x.Id]
                        .Select(c => new CandidatoConciliacaoResponse(c.ContaId, c.Diferenca, c.Pontos, c.Motivo, c.CorrespondenciaClara && !disputadas.Contains(c.ContaId))).ToArray()
                    : [], preferencias.GetValueOrDefault(EstabelecimentoKey.Normalizar(x.Descricao)),
                    x.RascunhoJson is null ? null : JsonSerializer.Deserialize<JsonElement>(x.RascunhoJson), x.UpdatedAtUtc)).ToArray(),
            contas.Select(x => new ContaConciliacaoResponse(x.Id, x.DataCompra ?? x.DataEmissao, x.Descricao, x.ValorLiquido,
                x.NumeroParcela, x.QuantidadeParcelas, x.ResponsavelCompraId, x.RecebedorId, x.FormaPagamentoId,
                x.RegraRecorrenciaId, x.GrupoReembolsoId, rateios.Where(r => r.ContaPagarId == x.Id).Select(r => new RateioConciliacaoResponse(r.ContaGerencialId, r.Valor)).ToArray())).ToArray());
    }

    public Task<bool> VincularAsync(Guid faturaId, Guid id, Guid itemId, VincularItemFaturaRequest request, CancellationToken ct)
        => atomic.ExecutarAsync(() => VincularCoreAsync(faturaId, id, itemId, request, ct), ct);

    private async Task<bool> VincularCoreAsync(Guid faturaId, Guid id, Guid itemId, VincularItemFaturaRequest request, CancellationToken ct)
    {
        var session = await db.Conciliacoes.Include(x => x.Itens).SingleOrDefaultAsync(x => x.Id == id && x.FaturaId == faturaId, ct);
        if (session is null) return false;
        var item = session.Itens.SingleOrDefault(x => x.Id == itemId);
        if (item is null) return false;
        if (item.StatusItem == StatusItemConciliacao.Conciliado && item.ContaPagarVinculadaId == request.ContaPagarId) return true;
        if (item.StatusItem != StatusItemConciliacao.Pendente) throw Erro("Este item já foi processado.");
        if (session.Itens.Any(x => x.Id != itemId && x.ContaPagarVinculadaId == request.ContaPagarId))
            throw Erro("Esta conta já foi vinculada a outro item desta fatura.");
        var fatura = await db.FaturasCartao.SingleAsync(x => x.Id == faturaId, ct);
        var conta = await db.ContasPagar
            .SingleOrDefaultAsync(x => x.Id == request.ContaPagarId && x.FaturaCartaoId == faturaId && x.CartaoId == fatura.CartaoId, ct);
        if (conta is null) throw Erro("Selecione uma conta desta fatura.");
        if (conta.StatusContaId == StatusConta.CanceladaId) throw Erro("Conta cancelada não pode ser conciliada.");
        if (conta.NumeroParcela != item.NumeroParcela || conta.QuantidadeParcelas != item.QuantidadeParcelas
            || Math.Sign(conta.ValorLiquido) != Math.Sign(item.Valor)) throw Erro("Parcela ou sinal incompatível com o item da fatura.");
        if (conta.ValorLiquido != request.ValorEsperadoSistema) throw Erro("O valor da conta mudou. Atualize a revisão antes de confirmar.");
        var anterior = conta.ValorLiquido;
        if (anterior != item.Valor)
        {
            if (!request.UsarValorFatura) throw Erro("Confirme o uso do valor da fatura para ajustar esta diferença.");
            if (fatura.Status != StatusFaturaCartao.Aberta || conta.StatusContaId == StatusConta.LiquidadaId)
                throw Erro("Não é permitido ajustar contas liquidadas ou faturas fechadas.");
        }
        // Valida o vínculo antes de alterar dinheiro. Uma única gravação mantém conta e decisão atômicas.
        session.ConciliarContaPagar(itemId, conta.Id, anterior);
        if (anterior != item.Valor)
        {
            var existentes = await db.RateiosContaGerencial.Where(x => x.ContaPagarId == conta.Id).ToListAsync(ct);
            if (existentes.Count == 0) throw Erro("A conta não possui rateio gerencial. Corrija o cadastro antes de conciliar.");
            var rateios = Ratear(existentes, anterior, item.Valor);
            conta.AjustarValorConciliacaoFatura(item.Valor, rateios);
            await helper.SincronizarRateiosContaAsync(conta, ct);
            fatura.AtualizarValorTotal(fatura.ValorTotal + item.Valor - anterior);
        }
        if (request.Reembolso is { } refund && !conta.GrupoReembolsoId.HasValue)
        {
            if (conta.ValorLiquido <= 0 || conta.StatusContaId == StatusConta.CanceladaId)
                throw Erro("Este lançamento não pode gerar reembolso.");
            await reembolsos.CriarReembolsoContaPagarAsync(new CriarReembolsoContaPagarRequest(conta.Id, refund.ParcelarIgual,
                refund.ValorTotal, refund.PagadoresIds, refund.FormaPagamentoId, refund.DataVencimento,
                refund.Descricao, refund.Observacao, refund.Rateios), ct);
        }
        await memoria.AprenderAsync(item, conta, request.Aprender, request.CamposParaAprender, ct);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<RevisaoFaturaResumoResponse>> ListarAsync(Guid faturaId, CancellationToken ct)
        => await db.Conciliacoes.AsNoTracking().Where(x => x.FaturaId == faturaId).OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new RevisaoFaturaResumoResponse(x.Id, x.NomeArquivo, x.Status.ToString(), x.CreatedAtUtc)).ToListAsync(ct);

    public async Task<RascunhoFaturaResponse?> SalvarRascunhoAsync(Guid faturaId, Guid id, Guid itemId, RascunhoFaturaRequest request, CancellationToken ct)
    {
        var session = await db.Conciliacoes.Include(x => x.Itens).SingleOrDefaultAsync(x => x.Id == id && x.FaturaId == faturaId, ct);
        var item = session?.Itens.SingleOrDefault(x => x.Id == itemId);
        if (item is null) return null;
        if (item.StatusItem != StatusItemConciliacao.Pendente) throw Erro("Item já processado.");
        if (request.AtualizadoEmUtc != item.UpdatedAtUtc) throw new DbUpdateConcurrencyException();
        if (request.Dados.ValueKind != JsonValueKind.Object || request.Dados.GetRawText().Length > 65536)
            throw Erro("Rascunho inválido ou muito grande.");
        session!.SalvarRascunho(itemId, request.Dados.GetRawText());
        await db.SaveChangesAsync(ct);
        // Return the persisted precision (PostgreSQL timestamps use microseconds).
        var persistedVersion = await db.ItensConciliacao.AsNoTracking().Where(x => x.Id == itemId)
            .Select(x => x.UpdatedAtUtc).SingleAsync(ct);
        return new(persistedVersion);
    }

    public async Task<IReadOnlyList<PreviaReembolsoFaturaResponse>?> PreviaReembolsoAsync(
        Guid faturaId, Guid id, Guid itemId, PreviaReembolsoFaturaRequest request, CancellationToken ct)
    {
        var session = await db.Conciliacoes.AsNoTracking().Include(x => x.Itens)
            .SingleOrDefaultAsync(x => x.Id == id && x.FaturaId == faturaId, ct);
        var item = session?.Itens.SingleOrDefault(x => x.Id == itemId);
        if (item is null) return null;
        if (item.StatusItem != StatusItemConciliacao.Pendente || item.Valor <= 0)
            throw Erro("Este item não pode gerar reembolso.");
        if (request.ValorTotal <= 0 || decimal.Round(request.ValorTotal, 2) != request.ValorTotal
            || request.PagadoresIds is null || request.PagadoresIds.Count == 0 || request.DataVencimento == default)
            throw Erro("Informe valor, pagadores e vencimento válidos para a prévia.");
        var pagadores = request.PagadoresIds.Distinct().ToArray();
        var nomes = await db.Pessoas.AsNoTracking().Where(x => pagadores.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Nome, ct);
        if (nomes.Count != pagadores.Length) throw Erro("Um ou mais pagadores não foram encontrados.");
        var fatura = await db.FaturasCartao.AsNoTracking().SingleAsync(x => x.Id == faturaId, ct);
        DateOnly[] vencimentos = [request.ParcelarIgual ? fatura.DataVencimento : request.DataVencimento];
        if (request.ContaPagarId is { } contaId)
        {
            var conta = await db.ContasPagar.AsNoTracking().SingleOrDefaultAsync(x => x.Id == contaId
                && x.FaturaCartaoId == faturaId && x.CartaoId == fatura.CartaoId, ct);
            if (conta is null || conta.GrupoReembolsoId.HasValue || conta.StatusContaId == StatusConta.CanceladaId
                || conta.ValorLiquido <= 0) throw Erro("Selecione uma conta válida desta fatura sem reembolso existente.");
            if (request.ParcelarIgual)
                vencimentos = conta.GrupoParcelamentoId is { } grupo
                    ? await db.ContasPagar.AsNoTracking().Where(x => x.GrupoParcelamentoId == grupo)
                        .OrderBy(x => x.NumeroParcela).Select(x => x.DataVencimento).ToArrayAsync(ct)
                    : [conta.DataVencimento];
        }
        var valoresPagadores = ParcelamentoHelper.Distribuir(request.ValorTotal, pagadores.Length).ToArray();
        var resultado = new List<PreviaReembolsoFaturaResponse>();
        for (var i = 0; i < pagadores.Length; i++)
        {
            var valores = ParcelamentoHelper.Distribuir(valoresPagadores[i], vencimentos.Length).ToArray();
            if (valores.Any(x => x <= 0)) throw Erro("O valor é insuficiente para dividir entre os pagadores e parcelas.");
            for (var j = 0; j < vencimentos.Length; j++)
                resultado.Add(new(pagadores[i], nomes[pagadores[i]], j + 1, vencimentos.Length, valores[j], vencimentos[j]));
        }
        return resultado;
    }

    public async Task<bool> IgnorarAsync(Guid faturaId, Guid id, Guid itemId, CancellationToken ct)
    {
        var session = await db.Conciliacoes.Include(x => x.Itens).SingleOrDefaultAsync(x => x.Id == id && x.FaturaId == faturaId, ct);
        var item = session?.Itens.SingleOrDefault(x => x.Id == itemId);
        if (item is null) return false;
        if (item.StatusItem == StatusItemConciliacao.Ignorado) return true;
        if (item.StatusItem != StatusItemConciliacao.Pendente) throw Erro("Não é possível ignorar um item já conciliado.");
        session!.IgnorarItem(itemId);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static CompraComparavel Comparavel(ContaPagar c) => new(c.Id, c.DataCompra ?? c.DataEmissao, c.Descricao, c.ValorLiquido, c.NumeroParcela, c.QuantidadeParcelas);
    private static Exception Erro(string message) => ValidationExceptionFactory.Create("Conciliacao", message);

    private static IReadOnlyCollection<RateioPlano> Ratear(IReadOnlyCollection<RateioContaGerencial> rateios, decimal anterior, decimal novo)
    {
        var ordered = rateios.OrderByDescending(x => Math.Abs(x.Valor)).ThenBy(x => x.ContaGerencialId).ToArray();
        var valores = ordered.Select(x => decimal.Truncate(x.Valor / anterior * novo * 100) / 100).ToArray();
        var restante = novo - valores.Sum();
        var centavo = Math.Sign(restante) * 0.01m;
        for (var i = 0; restante != 0; i = (i + 1) % valores.Length) { valores[i] += centavo; restante -= centavo; }
        return ordered.Select((r, i) => new RateioPlano(r.ContaGerencialId, valores[i])).Where(r => r.Valor != 0).ToArray();
    }
}
