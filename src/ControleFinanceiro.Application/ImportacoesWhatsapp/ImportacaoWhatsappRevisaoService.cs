using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.ImportacoesWhatsapp;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Cadastros.FormasPagamento;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.ImportacoesWhatsapp;

public interface IImportacaoWhatsappRevisaoService
{
    Task<ImportacaoWhatsappDetalheResponse?> ConfirmarItemAsync(
        Guid itemId,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken);

    Task<ImportacaoWhatsappDetalheResponse?> RejeitarItemAsync(
        Guid itemId,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken);
}

public sealed class ImportacaoWhatsappRevisaoService(
    IAppDbContext dbContext,
    IImportacaoWhatsappQueryService queryService,
    ImportacaoWhatsappSharedHelper helper) : IImportacaoWhatsappRevisaoService
{
    public async Task<ImportacaoWhatsappDetalheResponse?> ConfirmarItemAsync(
        Guid itemId,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken)
    {
        var importacao = await helper.CarregarImportacaoPorItemAsync(itemId, cancellationToken);
        if (importacao is null)
        {
            return null;
        }

        var item = importacao.Itens.Single(x => x.Id == itemId);
        var confirmacaoClassificada = await ValidarEClassificarConfirmacaoAsync(item, request, cancellationToken);

        try
        {
            ValidarImportacaoNaoAprovada(importacao);

            if (item.Status == StatusItemImportadoWhatsapp.Sugerido)
            {
                item.Confirmar(
                    request.Observacao,
                    confirmacaoClassificada.DescricaoAjustada,
                    request.ContaGerencialId,
                    request.ResponsavelId,
                    confirmacaoClassificada.ContaReceberId,
                    confirmacaoClassificada.MarcarComoRecorrente);
            }
            else if (item.Status == StatusItemImportadoWhatsapp.Confirmado)
            {
                item.AtualizarConfirmacao(
                    request.Observacao,
                    confirmacaoClassificada.DescricaoAjustada,
                    request.ContaGerencialId,
                    request.ResponsavelId,
                    confirmacaoClassificada.ContaReceberId,
                    confirmacaoClassificada.MarcarComoRecorrente);
            }
            else if (item.Status == StatusItemImportadoWhatsapp.Rejeitado)
            {
                item.ReabrirParaEdicao();
                item.Confirmar(
                    request.Observacao,
                    confirmacaoClassificada.DescricaoAjustada,
                    request.ContaGerencialId,
                    request.ResponsavelId,
                    confirmacaoClassificada.ContaReceberId,
                    confirmacaoClassificada.MarcarComoRecorrente);
            }
            else
            {
                throw new InvalidOperationException("Somente itens da importação em revisão podem ser confirmados.");
            }

            importacao.AtualizarStatusRevisao();
        }
        catch (InvalidOperationException exception)
        {
            throw ValidationExceptionFactory.Create("Status", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(importacao.Id, cancellationToken);
    }

    public async Task<ImportacaoWhatsappDetalheResponse?> RejeitarItemAsync(
        Guid itemId,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken)
    {
        var importacao = await helper.CarregarImportacaoPorItemAsync(itemId, cancellationToken);
        if (importacao is null)
        {
            return null;
        }

        var item = importacao.Itens.Single(x => x.Id == itemId);
        try
        {
            ValidarImportacaoNaoAprovada(importacao);

            if (item.Status != StatusItemImportadoWhatsapp.Sugerido)
            {
                item.ReabrirParaEdicao();
            }

            item.Rejeitar(request.Observacao);
            importacao.AtualizarStatusRevisao();
        }
        catch (InvalidOperationException exception)
        {
            throw ValidationExceptionFactory.Create("Status", exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await queryService.ObterPorIdAsync(importacao.Id, cancellationToken);
    }

    private async Task<ConfirmacaoClassificada> ValidarEClassificarConfirmacaoAsync(
        ItemImportadoWhatsapp item,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken)
    {
        var payload = ImportacaoWhatsappSuggestionPayload.Parse(item.PayloadSugeridoJson);
        var descricaoAjustada = SanitizarDescricaoAjustada(request.DescricaoAjustada, payload.Descricao);

        if (item.TipoSugestao == TipoSugestaoImportacaoWhatsapp.CompraCartao)
        {
            if (!request.ContaGerencialId.HasValue)
            {
                throw ValidationExceptionFactory.Create(
                    "ContaGerencialId",
                    "Conta gerencial é obrigatória para aprovar compras de cartão importadas.");
            }

            if (!request.ResponsavelId.HasValue)
            {
                throw ValidationExceptionFactory.Create(
                    "ResponsavelId",
                    "Responsável é obrigatório para aprovar compras de cartão importadas.");
            }
        }

        if (item.TipoSugestao != TipoSugestaoImportacaoWhatsapp.CompraCartao)
        {
            if (!string.IsNullOrWhiteSpace(descricaoAjustada))
            {
                throw ValidationExceptionFactory.Create(
                    "DescricaoAjustada",
                    "A renomeação amigável está disponível apenas para compras de cartão importadas.");
            }

            if (request.MarcarComoRecorrente)
            {
                throw ValidationExceptionFactory.Create(
                    "MarcarComoRecorrente",
                    "A recorrência por histórico está disponível apenas para compras de cartão importadas.");
            }
        }

        if (request.ResponsavelId.HasValue &&
            !await dbContext.Pessoas.AnyAsync(x => x.Id == request.ResponsavelId.Value, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("ResponsavelId", "Responsável não encontrado.");
        }

        if (request.ContaGerencialId.HasValue)
        {
            var tipoEsperado = item.TipoSugestao switch
            {
                TipoSugestaoImportacaoWhatsapp.ContaReceber => TipoContaGerencial.Receita,
                TipoSugestaoImportacaoWhatsapp.ContaPagar => TipoContaGerencial.Despesa,
                TipoSugestaoImportacaoWhatsapp.CompraCartao => TipoContaGerencial.Despesa,
                _ => (TipoContaGerencial?)null
            };

            if (tipoEsperado.HasValue)
            {
                await ContaGerencialLancamentoValidator.ValidarContaLancavelPorTipoAsync(
                    dbContext,
                    request.ContaGerencialId.Value,
                    tipoEsperado.Value,
                    "ContaGerencialId",
                    "Conta gerencial não encontrada.",
                    "Somente contas gerenciais filhas podem ser utilizadas na categorizacao.",
                    tipoEsperado.Value == TipoContaGerencial.Receita
                        ? "A categorizacao informada exige uma conta gerencial de receita."
                        : "A categorizacao informada exige uma conta gerencial de despesa.",
                    cancellationToken);
            }
            else
            {
                await ContaGerencialLancamentoValidator.ValidarContaLancavelAsync(
                    dbContext,
                    request.ContaGerencialId.Value,
                    "ContaGerencialId",
                    "Conta gerencial não encontrada.",
                    "Somente contas gerenciais filhas podem ser utilizadas na categorizacao.",
                    cancellationToken);
            }
        }

        if (!request.GerarContaReceber)
        {
            return new ConfirmacaoClassificada(null, descricaoAjustada, request.MarcarComoRecorrente);
        }

        if (item.TipoSugestao != TipoSugestaoImportacaoWhatsapp.CompraCartao)
        {
            throw ValidationExceptionFactory.Create(
                "GerarContaReceber",
                "A geração automática de conta a receber está disponível apenas para compras de cartão importadas.");
        }

        if (!request.ResponsavelId.HasValue)
        {
            throw ValidationExceptionFactory.Create(
                "ResponsavelId",
                "Responsável é obrigatório para gerar conta a receber a partir da fatura.");
        }

        if (string.IsNullOrWhiteSpace(payload.Descricao))
        {
            throw ValidationExceptionFactory.Create("Payload", "Não foi possível identificar a descrição do item importado.");
        }

        if (!payload.Valor.HasValue || payload.Valor.Value <= 0)
        {
            throw ValidationExceptionFactory.Create("Payload", "Não foi possível identificar um valor positivo para gerar a conta a receber.");
        }

        var dataVencimentoContaReceber = await ResolverDataVencimentoContaReceberAsync(payload, request, cancellationToken);
        if (!dataVencimentoContaReceber.HasValue)
        {
            throw ValidationExceptionFactory.Create(
                "DataVencimentoContaReceber",
                "Informe o vencimento do receber quando o documento não trouxer o vencimento da fatura.");
        }

        var contaGerencialPadrao = await dbContext.ContasGerenciais
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.EhPadraoRecebimentoFaturaCartao, cancellationToken);

        if (contaGerencialPadrao is null)
        {
            throw ValidationExceptionFactory.Create(
                "GerarContaReceber",
                "Nenhuma conta gerencial padrão para recebimento de fatura foi configurada.");
        }

        await ContaGerencialLancamentoValidator.ValidarContaLancavelPorTipoAsync(
            dbContext,
            contaGerencialPadrao.Id,
            TipoContaGerencial.Receita,
            "GerarContaReceber",
            "Conta gerencial padrão para recebimento de fatura não encontrada.",
            "A conta gerencial padrão para recebimento de fatura precisa ser lançável.",
            "A conta gerencial padrão para recebimento de fatura precisa ser do tipo receita.",
            cancellationToken);

        var formaPagamento = await dbContext.FormasPagamento
            .AsNoTracking()
            .Where(x => x.Ativo && !x.EhCartao)
            .OrderBy(x => x.Tipo == TipoFormaPagamento.Pix ? 0 : x.Tipo == TipoFormaPagamento.Transferencia ? 1 : 2)
            .ThenBy(x => x.Nome)
            .FirstOrDefaultAsync(cancellationToken);

        if (formaPagamento is null)
        {
            throw ValidationExceptionFactory.Create(
                "GerarContaReceber",
                "Cadastre ao menos uma forma de pagamento ativa não-cartão para gerar a conta a receber.");
        }

        var contaReceber = ContaReceber.Criar(
            numeroDocumento: null,
            dataEmissao: payload.DataIdentificada ?? DateOnly.FromDateTime(DateTime.UtcNow),
            responsavelId: request.ResponsavelId,
            pagadorId: request.ResponsavelId.Value,
            dataVencimento: dataVencimentoContaReceber.Value,
            formaPagamentoId: formaPagamento.Id,
            cartaoId: null,
            contaBancariaId: null,
            valorOriginal: payload.Valor.Value,
            valorDesconto: 0m,
            valorJuros: 0m,
            valorMulta: 0m,
            quantidadeParcelas: 1,
            numeroParcela: 1,
            grupoParcelamentoId: null,
            descricao: descricaoAjustada ?? payload.Descricao,
            observacao: "Gerada automaticamente a partir da revisão de item importado da fatura.",
            statusContaId: StatusConta.PendenteId,
            ehRecorrente: false,
            regraRecorrenciaId: null,
            origem: OrigemLancamento.Importacao,
            rateios:
            [
                RateioPlano.Create(contaGerencialPadrao.Id, payload.Valor.Value)
            ]);

        dbContext.ContasReceber.Add(contaReceber);
        dbContext.RateiosContaGerencial.AddRange(contaReceber.Rateios);

        return new ConfirmacaoClassificada(contaReceber.Id, descricaoAjustada, request.MarcarComoRecorrente);
    }

    private async Task<DateOnly?> ResolverDataVencimentoContaReceberAsync(
        ImportacaoWhatsappSuggestionPayload payload,
        RevisarItemImportadoWhatsappRequest request,
        CancellationToken cancellationToken)
    {
        if (payload.DataVencimento.HasValue)
        {
            return payload.DataVencimento.Value;
        }

        if (request.DataVencimentoContaReceber.HasValue)
        {
            return request.DataVencimentoContaReceber.Value;
        }

        if (string.IsNullOrWhiteSpace(payload.CartaoFinal))
        {
            return null;
        }

        var cartoes = await dbContext.Cartoes
            .AsNoTracking()
            .Where(x => x.Ativo && x.NumeroFinal == payload.CartaoFinal)
            .ToArrayAsync(cancellationToken);

        if (cartoes.Length != 1)
        {
            return null;
        }

        var dataCompra = payload.DataIdentificada ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return FaturaCartaoCompetencia.Calcular(
            dataCompra,
            cartoes[0].DiaFechamentoFatura,
            cartoes[0].DiaVencimentoFatura).DataVencimento;
    }

    private static string? SanitizarDescricaoAjustada(string? descricaoAjustada, string? descricaoOriginal)
    {
        if (string.IsNullOrWhiteSpace(descricaoAjustada))
        {
            return null;
        }

        var ajustada = descricaoAjustada.Trim();
        if (string.IsNullOrWhiteSpace(ajustada))
        {
            return null;
        }

        return string.Equals(ajustada, descricaoOriginal?.Trim(), StringComparison.OrdinalIgnoreCase)
            ? null
            : ajustada;
    }

    private static void ValidarImportacaoNaoAprovada(ImportacaoWhatsapp importacao)
    {
        if (importacao.Status == StatusImportacaoWhatsapp.Confirmado)
        {
            throw new InvalidOperationException("Importação aprovada não pode ser alterada. Reabra a importação para editar os itens.");
        }
    }
}
