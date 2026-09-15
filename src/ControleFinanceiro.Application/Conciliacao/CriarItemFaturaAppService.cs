using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Application.Financeiro.ContasPagar;
using ControleFinanceiro.Application.Financeiro.Reembolsos;
using ControleFinanceiro.Contracts.Conciliacao;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;
using ControleFinanceiro.Contracts.Financeiro.Reembolsos;
using ControleFinanceiro.Domain.Conciliacao;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Conciliacao;

public sealed class CriarItemFaturaAppService(IAppDbContext db, IAtomicOperation atomic, ContaPagarSharedHelper helper,
    IContaPagarRecorrenciaService recorrencias, IReembolsoAppService reembolsos, MemoriaEstabelecimentoAppService memoria)
{
    public Task<bool> CriarAsync(Guid faturaId, Guid sessionId, Guid itemId, CriarItemFaturaRequest request, CancellationToken ct)
        => atomic.ExecutarAsync(async () =>
        {
            var session = await db.Conciliacoes.Include(x => x.Itens).SingleOrDefaultAsync(x => x.Id == sessionId && x.FaturaId == faturaId, ct);
            if (session is null) return false;
            var item = session.Itens.SingleOrDefault(x => x.Id == itemId);
            if (item is null) return false;
            if (item.StatusItem == StatusItemConciliacao.Conciliado) return true;
            if (item.StatusItem != StatusItemConciliacao.Pendente) throw Erro("Este item já foi processado.");
            var fatura = await db.FaturasCartao.SingleAsync(x => x.Id == faturaId, ct);
            if (fatura.Status != StatusFaturaCartao.Aberta) throw Erro("Reabra a fatura antes de incluir lançamentos.");
            if (string.IsNullOrWhiteSpace(request.Descricao) || request.Descricao.Length > 200)
                throw Erro("Informe uma descrição com até 200 caracteres.");
            if (request.Rateios is null || request.Rateios.Count == 0 || request.Rateios.Sum(x => x.Valor) != item.Valor)
                throw Erro("Os rateios devem fechar o valor da parcela na fatura.");
            var chave = $"{fatura.CartaoId}|{item.ChaveOrigem}";
            var jaImportada = await db.ContasPagar.AnyAsync(x => x.CartaoId == fatura.CartaoId && x.ChaveSerieImportacaoCartao == chave, ct);
            var jaConciliada = await (from i in db.ItensConciliacao
                join s in db.Conciliacoes on i.ConciliacaoId equals s.Id
                where s.FaturaId == faturaId && i.ChaveOrigem == item.ChaveOrigem && i.ContaPagarVinculadaId != null
                select i.Id).AnyAsync(ct);
            if (jaImportada || jaConciliada) throw Erro("Este item já possui uma conta. Use o vínculo com o lançamento existente.");

            await helper.ValidarCriacaoOuAtualizacaoAsync(item.Data, request.RecebedorId, request.ResponsavelCompraId,
                request.FormaPagamentoId, fatura.CartaoId, null, null, item.QuantidadeParcelas, request.Rateios, ct, item.Data, faturaImportacaoId: faturaId);
            helper.ValidarRecorrencia(item.Data, request.Recorrencia, item.QuantidadeParcelas);
            if (item.Valor < 0 && (request.Recorrencia is not null || request.Reembolso is not null))
                throw Erro("Créditos de estorno não geram recorrência nem reembolso.");
            var template = new CriarContaPagarRequest(null, null, item.Data, request.ResponsavelCompraId, request.RecebedorId,
                fatura.DataVencimento, request.FormaPagamentoId, fatura.CartaoId, null, null, item.Valor, 0, 0, 0,
                item.QuantidadeParcelas, request.Descricao, request.Observacao, request.Rateios, request.Recorrencia, item.Data);
            var regra = request.Recorrencia is null ? null : helper.CriarRegraRecorrencia(template, request.Recorrencia);
            if (regra is not null) db.RegrasRecorrencia.Add(regra);
            // O PDF descreve esta parcela; não dividir novamente o valor nem recriar parcelas anteriores.
            var conta = ContaPagar.Criar(null, item.Data, request.ResponsavelCompraId, request.RecebedorId, fatura.DataVencimento,
                request.FormaPagamentoId, fatura.CartaoId, null, item.Valor, 0, 0, 0, item.QuantidadeParcelas, item.NumeroParcela,
                null, null, request.Descricao, request.Observacao, StatusConta.EmFaturaId, regra is not null, regra?.Id,
                OrigemLancamento.Importacao, request.Rateios.Select(x => RateioPlano.CreateSigned(x.ContaGerencialId, x.Valor)).ToArray(), item.Data);
            conta.VincularFaturaCartao(faturaId);
            conta.DefinirChaveSerieImportacaoCartao(chave);
            db.ContasPagar.Add(conta); db.RateiosContaGerencial.AddRange(conta.Rateios);
            session.ConciliarContaPagar(itemId, conta.Id, null);
            await db.SaveChangesAsync(ct);
            if (regra is not null)
            {
                var horizonte = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(6);
                await recorrencias.GerarPorRegraAsync(regra, new DateOnly(horizonte.Year, horizonte.Month, DateTime.DaysInMonth(horizonte.Year, horizonte.Month)), ct);
            }
            if (request.Reembolso is { } refund)
            {
                await reembolsos.CriarReembolsoContaPagarAsync(new CriarReembolsoContaPagarRequest(conta.Id, refund.ParcelarIgual,
                    refund.ValorTotal, refund.PagadoresIds, refund.FormaPagamentoId, refund.DataVencimento, refund.Descricao,
                    refund.Observacao, refund.Rateios), ct);
            }
            await memoria.AprenderAsync(item, conta, request.Aprender, request.CamposParaAprender, ct);
            var valores = await db.ContasPagar.Where(x => x.FaturaCartaoId == faturaId && x.StatusContaId != StatusConta.CanceladaId)
                .Select(x => x.ValorLiquido).ToListAsync(ct);
            fatura.AtualizarValorTotal(valores.Sum());
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);

    private static Exception Erro(string message) => ValidationExceptionFactory.Create("Conciliacao", message);
}
