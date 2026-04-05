using System.Text.Json;
using ControleFinanceiro.Application.Common.Exceptions;
using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Application.Financeiro.Recorrencias;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Contracts.Financeiro.Common;
using ControleFinanceiro.Contracts.Financeiro.ContasPagar;
using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Cadastros.ContasBancarias;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using ControleFinanceiro.Domain.Cadastros.FormasPagamento;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.Financeiro;
using Microsoft.EntityFrameworkCore;
using TipoPeriodicidadeRecorrenciaContract = ControleFinanceiro.Contracts.Financeiro.Common.TipoPeriodicidadeRecorrencia;
using TipoPeriodicidadeRecorrenciaDomain = ControleFinanceiro.Domain.Financeiro.TipoPeriodicidadeRecorrencia;

namespace ControleFinanceiro.Application.Financeiro.ContasPagar;

public sealed class ContaPagarAppService(IAppDbContext dbContext)
{
    public async Task<PagedResult<ContaPagarResumoResponse>> ListarAsync(
        ContaPagarListQueryRequest query,
        CancellationToken cancellationToken)
    {
        var consulta =
            from conta in dbContext.ContasPagar.AsNoTracking()
            join recebedor in dbContext.Pessoas.AsNoTracking() on conta.RecebedorId equals recebedor.Id
            join forma in dbContext.FormasPagamento.AsNoTracking() on conta.FormaPagamentoId equals forma.Id
            join status in dbContext.StatusContas.AsNoTracking() on conta.StatusContaId equals status.Id
            select new
            {
                conta.Id,
                conta.NumeroDocumento,
                conta.Descricao,
                conta.RecebedorId,
                RecebedorNome = recebedor.Nome,
                conta.DataEmissao,
                conta.DataVencimento,
                conta.DataLiquidacao,
                conta.FormaPagamentoId,
                FormaPagamentoNome = forma.Nome,
                conta.ValorLiquido,
                StatusCodigo = status.Codigo,
                StatusNome = status.Nome,
                conta.QuantidadeParcelas,
                conta.NumeroParcela,
                conta.GrupoParcelamentoId,
                conta.EhRecorrente
            };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = query.Search.Trim().ToLower();
            consulta = consulta.Where(x =>
                x.Descricao.ToLower().Contains(termo) ||
                (x.NumeroDocumento != null && x.NumeroDocumento.ToLower().Contains(termo)) ||
                x.RecebedorNome.ToLower().Contains(termo));
        }

        if (query.RecebedorId.HasValue)
        {
            consulta = consulta.Where(x => x.RecebedorId == query.RecebedorId.Value);
        }

        if (query.FormaPagamentoId.HasValue)
        {
            consulta = consulta.Where(x => x.FormaPagamentoId == query.FormaPagamentoId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.StatusCodigo))
        {
            var statusCodigo = query.StatusCodigo.Trim().ToUpperInvariant();
            consulta = consulta.Where(x => x.StatusCodigo == statusCodigo);
        }

        if (query.DataVencimentoInicial.HasValue)
        {
            consulta = consulta.Where(x => x.DataVencimento >= query.DataVencimentoInicial.Value);
        }

        if (query.DataVencimentoFinal.HasValue)
        {
            consulta = consulta.Where(x => x.DataVencimento <= query.DataVencimentoFinal.Value);
        }

        consulta = (query.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "valorliquido" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.ValorLiquido).ThenByDescending(x => x.DataVencimento)
                : consulta.OrderBy(x => x.ValorLiquido).ThenBy(x => x.DataVencimento),
            "datavencimento" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.DataVencimento).ThenByDescending(x => x.Descricao)
                : consulta.OrderBy(x => x.DataVencimento).ThenBy(x => x.Descricao),
            _ => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.DataVencimento).ThenByDescending(x => x.Descricao)
                : consulta.OrderBy(x => x.DataVencimento).ThenBy(x => x.Descricao)
        };

        var totalItems = await consulta.CountAsync(cancellationToken);
        var items = (await consulta
                .ApplyPagination(query)
                .ToArrayAsync(cancellationToken))
            .Select(x => new ContaPagarResumoResponse(
                x.Id,
                x.NumeroDocumento,
                x.Descricao,
                x.RecebedorId,
                x.RecebedorNome,
                x.DataEmissao,
                x.DataVencimento,
                x.DataLiquidacao,
                x.FormaPagamentoId,
                x.FormaPagamentoNome,
                x.ValorLiquido,
                x.StatusCodigo,
                x.StatusNome,
                x.QuantidadeParcelas,
                x.NumeroParcela,
                x.GrupoParcelamentoId,
                x.EhRecorrente))
            .ToArray();

        return PagedResult<ContaPagarResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);
    }

    public async Task<ContaPagarDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return conta is null ? null : await MapearDetalheAsync(conta, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse> CriarAsync(CriarContaPagarRequest request, CancellationToken cancellationToken)
    {
        ValidarRecorrencia(request.Recorrencia, request.QuantidadeParcelas);

        var contexto = await ValidarCriacaoOuAtualizacaoAsync(
            request.DataEmissao,
            request.RecebedorId,
            request.ResponsavelCompraId,
            request.FormaPagamentoId,
            request.CartaoId,
            request.ContaBancariaId,
            request.DataLiquidacao,
            request.QuantidadeParcelas,
            request.Rateios,
            cancellationToken);

        RegraRecorrencia? regra = null;
        if (request.Recorrencia is not null)
        {
            regra = CriarRegraRecorrencia(request, request.Recorrencia);
            dbContext.RegrasRecorrencia.Add(regra);
        }

        var contas = ContaPagar.CriarParcelas(
            request.NumeroDocumento,
            request.DataEmissao,
            request.ResponsavelCompraId,
            request.RecebedorId,
            request.DataVencimento,
            request.FormaPagamentoId,
            request.CartaoId,
            request.ContaBancariaId,
            request.ValorOriginal,
            request.ValorDesconto,
            request.ValorJuros,
            request.ValorMulta,
            request.QuantidadeParcelas,
            request.Descricao,
            request.Observacao,
            StatusConta.PendenteId,
            regra is not null,
            regra?.Id,
            OrigemLancamento.Manual,
            ConverterRateios(request.Rateios));

        dbContext.ContasPagar.AddRange(contas);
        dbContext.RateiosContaGerencial.AddRange(contas.SelectMany(x => x.Rateios));

        if (contexto.LiquidarNaCriacao)
        {
            dbContext.MovimentacoesFinanceiras.AddRange(AplicarLiquidacaoAutomatica(contas, request.DataLiquidacao, request.ContaBancariaId!.Value));
        }
        else if (contexto.CompraCartao)
        {
            dbContext.MovimentacoesFinanceiras.AddRange(CriarMovimentacoesEconomicasCompraCartao(contas));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await MapearDetalheAsync(contas.First(), cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> AtualizarAsync(
        Guid id,
        AtualizarContaPagarRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        if (request.QuantidadeParcelas != conta.QuantidadeParcelas)
        {
            throw ValidationExceptionFactory.Create("QuantidadeParcelas", "Nao e permitido alterar o parcelamento na edicao.");
        }

        if (conta.RegraRecorrenciaId.HasValue)
        {
            var regra = await dbContext.RegrasRecorrencia
                .SingleAsync(x => x.Id == conta.RegraRecorrenciaId.Value, cancellationToken);

            if (!regra.PermiteEdicaoOcorrenciaIndividual)
            {
                throw ValidationExceptionFactory.Create("Recorrencia", "A regra atual nao permite edicao pontual da ocorrencia.");
            }
        }

        var contexto = await ValidarCriacaoOuAtualizacaoAsync(
            request.DataEmissao,
            request.RecebedorId,
            request.ResponsavelCompraId,
            request.FormaPagamentoId,
            request.CartaoId,
            request.ContaBancariaId,
            request.DataLiquidacao,
            request.QuantidadeParcelas,
            request.Rateios,
            cancellationToken);

        AtualizarContaExistente(conta, request);

        await SincronizarRateiosContaAsync(conta, cancellationToken);

        if (contexto.LiquidarNaCriacao &&
            !await dbContext.MovimentacoesFinanceiras.AnyAsync(x => x.ContaPagarId == conta.Id, cancellationToken))
        {
            dbContext.MovimentacoesFinanceiras.AddRange(
                AplicarLiquidacaoAutomatica([conta], request.DataLiquidacao, request.ContaBancariaId!.Value));
        }

        await SincronizarMovimentacaoEconomicaAsync(conta, contexto.CompraCartao, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await MapearDetalheAsync(conta, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> AlterarFuturasAsync(
        Guid id,
        AtualizarContaPagarRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        var regra = await ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);
        ValidarRecorrencia(request.Recorrencia, request.QuantidadeParcelas);

        var recorrencia = request.Recorrencia ?? new RecorrenciaConfigRequest(
            MapearTipoPeriodicidadeContrato(regra.TipoPeriodicidade),
            regra.DiaGeracaoMensal,
            regra.DataInicio,
            regra.DataFim,
            regra.PermiteEdicaoOcorrenciaIndividual,
            regra.Observacao);

        await ValidarCriacaoOuAtualizacaoAsync(
            request.DataEmissao,
            request.RecebedorId,
            request.ResponsavelCompraId,
            request.FormaPagamentoId,
            request.CartaoId,
            request.ContaBancariaId,
            request.DataLiquidacao,
            request.QuantidadeParcelas,
            request.Rateios,
            cancellationToken);

        regra.Atualizar(
            MapearTipoPeriodicidadeDominio(recorrencia.TipoPeriodicidade),
            recorrencia.DiaGeracaoMensal,
            recorrencia.DataInicio,
            recorrencia.DataFim,
            recorrencia.PermiteEdicaoOcorrenciaIndividual,
            recorrencia.Observacao,
            SerializarTemplate(request));

        var contasFuturas = await dbContext.ContasPagar
            .Where(x =>
                x.RegraRecorrenciaId == regra.Id &&
                x.DataVencimento >= conta.DataVencimento &&
                x.StatusContaId != StatusConta.LiquidadaId &&
                x.StatusContaId != StatusConta.CanceladaId)
            .OrderBy(x => x.DataVencimento)
            .ToListAsync(cancellationToken);

        foreach (var contaFutura in contasFuturas)
        {
            var mesOffset = RecorrenciaDateHelper.CalculateMonthOffset(conta.DataVencimento, contaFutura.DataVencimento);
            var requestAjustado = AjustarRequestParaMes(request, mesOffset);

            AtualizarContaExistente(contaFutura, requestAjustado);
            await SincronizarRateiosContaAsync(contaFutura, cancellationToken);

            var contexto = await ValidarCriacaoOuAtualizacaoAsync(
                requestAjustado.DataEmissao,
                requestAjustado.RecebedorId,
                requestAjustado.ResponsavelCompraId,
                requestAjustado.FormaPagamentoId,
                requestAjustado.CartaoId,
                requestAjustado.ContaBancariaId,
                requestAjustado.DataLiquidacao,
                requestAjustado.QuantidadeParcelas,
                requestAjustado.Rateios,
                cancellationToken);

            await SincronizarMovimentacaoEconomicaAsync(contaFutura, contexto.CompraCartao, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapearDetalheAsync(conta, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> GerarOcorrenciasAsync(
        Guid id,
        GerarOcorrenciasRecorrenciaRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        var regra = await ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);

        if (!regra.Ativa)
        {
            throw ValidationExceptionFactory.Create("Recorrencia", "A recorrencia esta pausada ou encerrada.");
        }

        var datasExistentes = await dbContext.ContasPagar
            .Where(x => x.RegraRecorrenciaId == regra.Id)
            .Select(x => x.DataVencimento)
            .ToArrayAsync(cancellationToken);

        var datasPendentes = regra.CalcularDatasPendentes(datasExistentes, request.AteData);
        var template = DesserializarTemplate(regra.TemplateJson);

        var novasContas = datasPendentes
            .Select(dataVencimento => CriarOcorrenciaRecorrente(template, regra.Id, dataVencimento))
            .ToArray();

        dbContext.ContasPagar.AddRange(novasContas);
        dbContext.RateiosContaGerencial.AddRange(novasContas.SelectMany(x => x.Rateios));

        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapearDetalheAsync(conta, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> PausarRecorrenciaAsync(Guid id, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        var regra = await ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);
        regra.Pausar();

        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapearDetalheAsync(conta, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> EncerrarRecorrenciaAsync(
        Guid id,
        EncerrarRecorrenciaRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        var regra = await ObterRegraRecorrenciaObrigatoriaAsync(conta, cancellationToken);
        regra.Encerrar(request.DataFim);

        var contasPosteriores = await dbContext.ContasPagar
            .Where(x =>
                x.RegraRecorrenciaId == regra.Id &&
                x.DataVencimento > request.DataFim &&
                x.StatusContaId != StatusConta.LiquidadaId &&
                x.StatusContaId != StatusConta.CanceladaId)
            .ToListAsync(cancellationToken);

        foreach (var contaPosterior in contasPosteriores)
        {
            contaPosterior.Cancelar(StatusConta.CanceladaId);

            var movimentoEconomico = await dbContext.MovimentacoesFinanceiras
                .SingleOrDefaultAsync(
                    x => x.ContaPagarId == contaPosterior.Id && x.Natureza == NaturezaMovimentacao.Economica,
                    cancellationToken);

            movimentoEconomico?.Cancelar(StatusMovimentacao.CanceladaId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapearDetalheAsync(conta, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> LiquidarAsync(
        Guid id,
        LiquidarContaPagarRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        if (conta.StatusContaId == StatusConta.LiquidadaId)
        {
            throw ValidationExceptionFactory.Create("Status", "Conta ja esta liquidada.");
        }

        if (!await dbContext.ContasBancarias.AnyAsync(x => x.Id == request.ContaBancariaId, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("ContaBancariaId", "Conta bancaria nao encontrada.");
        }

        var formaPagamento = await dbContext.FormasPagamento
            .AsNoTracking()
            .SingleAsync(x => x.Id == conta.FormaPagamentoId, cancellationToken);

        if (formaPagamento.EhCartao || conta.CartaoId.HasValue)
        {
            throw ValidationExceptionFactory.Create("CartaoId", "Compras em cartao devem ser liquidadas pela fatura.");
        }

        conta.Liquidar(request.DataLiquidacao, request.ContaBancariaId, StatusConta.LiquidadaId);
        dbContext.MovimentacoesFinanceiras.Add(
            MovimentacaoFinanceira.CriarLiquidacaoContaPagar(
                conta.Id,
                request.ContaBancariaId,
                request.DataLiquidacao,
                conta.ValorLiquido,
                StatusMovimentacao.EfetivadaId,
                conta.Descricao));

        await dbContext.SaveChangesAsync(cancellationToken);

        return await MapearDetalheAsync(conta, cancellationToken);
    }

    public async Task<ContaPagarDetalheResponse?> CancelarAsync(Guid id, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasPagar.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        try
        {
            conta.Cancelar(StatusConta.CanceladaId);
        }
        catch (InvalidOperationException exception)
        {
            throw ConverterParaValidacao(exception);
        }

        var movimentoEconomico = await dbContext.MovimentacoesFinanceiras
            .SingleOrDefaultAsync(
                x => x.ContaPagarId == conta.Id && x.Natureza == NaturezaMovimentacao.Economica,
                cancellationToken);

        movimentoEconomico?.Cancelar(StatusMovimentacao.CanceladaId);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapearDetalheAsync(conta, cancellationToken);
    }

    private async Task<ContaPagarDetalheResponse> MapearDetalheAsync(ContaPagar conta, CancellationToken cancellationToken)
    {
        var recebedor = await dbContext.Pessoas.AsNoTracking().SingleAsync(x => x.Id == conta.RecebedorId, cancellationToken);
        var responsavel = conta.ResponsavelCompraId.HasValue
            ? await dbContext.Pessoas.AsNoTracking().SingleOrDefaultAsync(x => x.Id == conta.ResponsavelCompraId.Value, cancellationToken)
            : null;
        var formaPagamento = await dbContext.FormasPagamento.AsNoTracking().SingleAsync(x => x.Id == conta.FormaPagamentoId, cancellationToken);
        var cartao = conta.CartaoId.HasValue
            ? await dbContext.Cartoes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == conta.CartaoId.Value, cancellationToken)
            : null;
        var contaBancaria = conta.ContaBancariaId.HasValue
            ? await dbContext.ContasBancarias.AsNoTracking().SingleOrDefaultAsync(x => x.Id == conta.ContaBancariaId.Value, cancellationToken)
            : null;
        var status = await dbContext.StatusContas.AsNoTracking().SingleAsync(x => x.Id == conta.StatusContaId, cancellationToken);
        var regra = conta.RegraRecorrenciaId.HasValue
            ? await dbContext.RegrasRecorrencia.AsNoTracking().SingleOrDefaultAsync(x => x.Id == conta.RegraRecorrenciaId.Value, cancellationToken)
            : null;

        var rateios = await (
            from rateio in dbContext.RateiosContaGerencial.AsNoTracking()
            join contaGerencial in dbContext.ContasGerenciais.AsNoTracking() on rateio.ContaGerencialId equals contaGerencial.Id
            where rateio.ContaPagarId == conta.Id
            orderby contaGerencial.Descricao
            select new RateioResponse(
                rateio.Id,
                rateio.ContaGerencialId,
                contaGerencial.Codigo,
                contaGerencial.Descricao,
                rateio.Valor,
                rateio.Percentual))
            .ToArrayAsync(cancellationToken);

        return new ContaPagarDetalheResponse(
            conta.Id,
            conta.NumeroDocumento,
            conta.DataEmissao,
            conta.ResponsavelCompraId,
            responsavel?.Nome,
            conta.RecebedorId,
            recebedor.Nome,
            conta.DataVencimento,
            conta.DataLiquidacao,
            conta.FormaPagamentoId,
            formaPagamento.Nome,
            formaPagamento.EhCartao,
            formaPagamento.BaixarAutomaticamente,
            conta.CartaoId,
            cartao?.Nome,
            conta.ContaBancariaId,
            contaBancaria?.Nome,
            conta.ValorOriginal,
            conta.ValorDesconto,
            conta.ValorJuros,
            conta.ValorMulta,
            conta.ValorLiquido,
            conta.QuantidadeParcelas,
            conta.NumeroParcela,
            conta.GrupoParcelamentoId,
            conta.Descricao,
            conta.Observacao,
            status.Codigo,
            status.Nome,
            conta.EhRecorrente,
            MapearOrigem(conta.Origem),
            MapearRecorrencia(regra),
            rateios,
            conta.CreatedAtUtc,
            conta.UpdatedAtUtc);
    }

    private async Task<ContaPagarValidationContext> ValidarCriacaoOuAtualizacaoAsync(
        DateOnly dataEmissao,
        Guid recebedorId,
        Guid? responsavelCompraId,
        Guid formaPagamentoId,
        Guid? cartaoId,
        Guid? contaBancariaId,
        DateOnly? dataLiquidacao,
        int quantidadeParcelas,
        IReadOnlyCollection<RateioRequest> rateios,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Pessoas.AnyAsync(x => x.Id == recebedorId, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("RecebedorId", "Recebedor nao encontrado.");
        }

        if (responsavelCompraId.HasValue &&
            !await dbContext.Pessoas.AnyAsync(x => x.Id == responsavelCompraId.Value, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("ResponsavelCompraId", "Responsavel nao encontrado.");
        }

        var formaPagamento = await dbContext.FormasPagamento
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == formaPagamentoId, cancellationToken);

        if (formaPagamento is null)
        {
            throw ValidationExceptionFactory.Create("FormaPagamentoId", "Forma de pagamento nao encontrada.");
        }

        if (quantidadeParcelas < 1)
        {
            throw ValidationExceptionFactory.Create("QuantidadeParcelas", "Quantidade de parcelas deve ser maior que zero.");
        }

        Cartao? cartao = null;
        if (cartaoId.HasValue)
        {
            cartao = await dbContext.Cartoes
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == cartaoId.Value, cancellationToken);

            if (cartao is null)
            {
                throw ValidationExceptionFactory.Create("CartaoId", "Cartao nao encontrado.");
            }
        }

        if (contaBancariaId.HasValue &&
            !await dbContext.ContasBancarias.AnyAsync(x => x.Id == contaBancariaId.Value, cancellationToken))
        {
            throw ValidationExceptionFactory.Create("ContaBancariaId", "Conta bancaria nao encontrada.");
        }

        var idsContasGerenciais = rateios.Select(x => x.ContaGerencialId).Distinct().ToArray();
        var contasGerenciaisEncontradas = await dbContext.ContasGerenciais
            .Where(x => idsContasGerenciais.Contains(x.Id))
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);

        if (contasGerenciaisEncontradas.Length != idsContasGerenciais.Length)
        {
            throw ValidationExceptionFactory.Create("Rateios", "Uma ou mais contas gerenciais nao foram encontradas.");
        }

        if (formaPagamento.EhCartao)
        {
            if (!cartaoId.HasValue)
            {
                throw ValidationExceptionFactory.Create("CartaoId", "Cartao e obrigatorio para compras em cartao.");
            }

            if (contaBancariaId.HasValue)
            {
                throw ValidationExceptionFactory.Create("ContaBancariaId", "Compras em cartao nao geram saida bancaria real neste momento.");
            }

            if (dataLiquidacao.HasValue)
            {
                throw ValidationExceptionFactory.Create("DataLiquidacao", "Compras em cartao nao devem informar data de liquidacao.");
            }

            if (formaPagamento.BaixarAutomaticamente)
            {
                throw ValidationExceptionFactory.Create("FormaPagamentoId", "Forma de pagamento de cartao nao pode baixar automaticamente.");
            }

            var competencia = FaturaCartaoCompetencia.Calcular(
                dataEmissao,
                cartao!.DiaFechamentoFatura,
                cartao.DiaVencimentoFatura);

            if (await dbContext.FaturasCartao.AnyAsync(
                    x => x.CartaoId == cartaoId.Value &&
                         x.Competencia == competencia.Competencia &&
                         x.Status == StatusFaturaCartao.Paga,
                    cancellationToken))
            {
                throw ValidationExceptionFactory.Create("DataEmissao", "Ja existe fatura paga para a competencia desta compra em cartao.");
            }
        }
        else if (cartaoId.HasValue)
        {
            throw ValidationExceptionFactory.Create("CartaoId", "Cartao informado para uma forma de pagamento que nao e cartao.");
        }

        if (formaPagamento.BaixarAutomaticamente && !contaBancariaId.HasValue)
        {
            throw ValidationExceptionFactory.Create("ContaBancariaId", "Conta bancaria e obrigatoria para baixa automatica.");
        }

        if (!formaPagamento.BaixarAutomaticamente && dataLiquidacao.HasValue)
        {
            throw ValidationExceptionFactory.Create("DataLiquidacao", "Data de liquidacao so pode ser informada com baixa automatica.");
        }

        return new ContaPagarValidationContext(
            formaPagamento.BaixarAutomaticamente && !formaPagamento.EhCartao,
            formaPagamento.EhCartao);
    }

    private static IReadOnlyCollection<RateioPlano> ConverterRateios(IReadOnlyCollection<RateioRequest> rateios)
    {
        try
        {
            return rateios.Select(x => RateioPlano.Create(x.ContaGerencialId, x.Valor)).ToArray();
        }
        catch (ArgumentException exception)
        {
            throw ValidationExceptionFactory.Create("Rateios", exception.Message);
        }
    }

    private static IReadOnlyCollection<MovimentacaoFinanceira> AplicarLiquidacaoAutomatica(
        IReadOnlyCollection<ContaPagar> contas,
        DateOnly? dataLiquidacao,
        Guid contaBancariaId)
    {
        var movimentos = new List<MovimentacaoFinanceira>(contas.Count);

        foreach (var conta in contas)
        {
            var dataMovimentacao = (dataLiquidacao ?? conta.DataEmissao).AddMonths(conta.NumeroParcela - 1);
            conta.Liquidar(dataMovimentacao, contaBancariaId, StatusConta.LiquidadaId);
            movimentos.Add(MovimentacaoFinanceira.CriarLiquidacaoContaPagar(
                conta.Id,
                contaBancariaId,
                dataMovimentacao,
                conta.ValorLiquido,
                StatusMovimentacao.EfetivadaId,
                conta.Descricao));
        }

        return movimentos;
    }

    private static IReadOnlyCollection<MovimentacaoFinanceira> CriarMovimentacoesEconomicasCompraCartao(
        IReadOnlyCollection<ContaPagar> contas)
    {
        return contas.Select(CriarMovimentacaoEconomicaCompraCartao).ToArray();
    }

    private static MovimentacaoFinanceira CriarMovimentacaoEconomicaCompraCartao(ContaPagar conta)
    {
        return MovimentacaoFinanceira.CriarCompraCartaoEconomica(
            conta.Id,
            conta.DataEmissao,
            conta.ValorLiquido,
            StatusMovimentacao.EfetivadaId,
            conta.Descricao);
    }

    private static ApplicationValidationException ConverterParaValidacao(Exception exception)
    {
        return ValidationExceptionFactory.Create("Request", exception.Message);
    }

    private static TipoPeriodicidadeRecorrenciaDomain MapearTipoPeriodicidadeDominio(TipoPeriodicidadeRecorrenciaContract tipo)
    {
        return tipo switch
        {
            TipoPeriodicidadeRecorrenciaContract.Mensal => TipoPeriodicidadeRecorrenciaDomain.Mensal,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }

    private static TipoPeriodicidadeRecorrenciaContract MapearTipoPeriodicidadeContrato(TipoPeriodicidadeRecorrenciaDomain tipo)
    {
        return tipo switch
        {
            TipoPeriodicidadeRecorrenciaDomain.Mensal => TipoPeriodicidadeRecorrenciaContract.Mensal,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }

    private static void ValidarRecorrencia(RecorrenciaConfigRequest? recorrencia, int quantidadeParcelas)
    {
        if (recorrencia is not null && quantidadeParcelas != 1)
        {
            throw ValidationExceptionFactory.Create("QuantidadeParcelas", "Recorrencia inicial nao pode ser combinada com parcelamento.");
        }
    }

    private RegraRecorrencia CriarRegraRecorrencia(CriarContaPagarRequest request, RecorrenciaConfigRequest recorrencia)
    {
        return RegraRecorrencia.Criar(
            TipoLancamentoRecorrencia.ContaPagar,
            MapearTipoPeriodicidadeDominio(recorrencia.TipoPeriodicidade),
            recorrencia.DiaGeracaoMensal,
            recorrencia.DataInicio,
            recorrencia.DataFim,
            recorrencia.PermiteEdicaoOcorrenciaIndividual,
            recorrencia.Observacao,
            SerializarTemplate(request));
    }

    private static string SerializarTemplate(CriarContaPagarRequest request)
    {
        return JsonSerializer.Serialize(new ContaPagarRecorrenciaTemplate(
            request.NumeroDocumento,
            request.DataEmissao,
            request.ResponsavelCompraId,
            request.RecebedorId,
            request.DataVencimento,
            request.FormaPagamentoId,
            request.CartaoId,
            request.ContaBancariaId,
            request.ValorOriginal,
            request.ValorDesconto,
            request.ValorJuros,
            request.ValorMulta,
            request.Descricao,
            request.Observacao,
            request.Rateios.Select(x => new RateioRecorrenciaTemplate(x.ContaGerencialId, x.Valor)).ToArray()));
    }

    private static string SerializarTemplate(AtualizarContaPagarRequest request)
    {
        return JsonSerializer.Serialize(new ContaPagarRecorrenciaTemplate(
            request.NumeroDocumento,
            request.DataEmissao,
            request.ResponsavelCompraId,
            request.RecebedorId,
            request.DataVencimento,
            request.FormaPagamentoId,
            request.CartaoId,
            request.ContaBancariaId,
            request.ValorOriginal,
            request.ValorDesconto,
            request.ValorJuros,
            request.ValorMulta,
            request.Descricao,
            request.Observacao,
            request.Rateios.Select(x => new RateioRecorrenciaTemplate(x.ContaGerencialId, x.Valor)).ToArray()));
    }

    private static ContaPagarRecorrenciaTemplate DesserializarTemplate(string templateJson)
    {
        return JsonSerializer.Deserialize<ContaPagarRecorrenciaTemplate>(templateJson)
               ?? throw new InvalidOperationException("Template de recorrencia invalido.");
    }

    private static AtualizarContaPagarRequest AjustarRequestParaMes(AtualizarContaPagarRequest request, int monthOffset)
    {
        return request with
        {
            DataEmissao = RecorrenciaDateHelper.Shift(request.DataEmissao, monthOffset),
            DataVencimento = RecorrenciaDateHelper.Shift(request.DataVencimento, monthOffset)
        };
    }

    private static LancamentoOrigem MapearOrigem(OrigemLancamento origem)
    {
        return origem switch
        {
            OrigemLancamento.Manual => LancamentoOrigem.Manual,
            OrigemLancamento.Recorrencia => LancamentoOrigem.Recorrencia,
            OrigemLancamento.Importacao => LancamentoOrigem.Importacao,
            _ => throw new ArgumentOutOfRangeException(nameof(origem))
        };
    }

    private static ContaPagar CriarOcorrenciaRecorrente(
        ContaPagarRecorrenciaTemplate template,
        Guid regraRecorrenciaId,
        DateOnly dataVencimento)
    {
        var monthOffset = RecorrenciaDateHelper.CalculateMonthOffset(template.DataVencimento, dataVencimento);

        return ContaPagar.Criar(
            template.NumeroDocumento,
            RecorrenciaDateHelper.Shift(template.DataEmissao, monthOffset),
            template.ResponsavelCompraId,
            template.RecebedorId,
            dataVencimento,
            template.FormaPagamentoId,
            template.CartaoId,
            template.ContaBancariaId,
            template.ValorOriginal,
            template.ValorDesconto,
            template.ValorJuros,
            template.ValorMulta,
            1,
            1,
            null,
            template.Descricao,
            template.Observacao,
            StatusConta.PendenteId,
            true,
            regraRecorrenciaId,
            OrigemLancamento.Recorrencia,
            template.Rateios.Select(x => RateioPlano.Create(x.ContaGerencialId, x.Valor)).ToArray());
    }

    private static void AtualizarContaExistente(ContaPagar conta, AtualizarContaPagarRequest request)
    {
        try
        {
            conta.Atualizar(
                request.NumeroDocumento,
                request.DataEmissao,
                request.ResponsavelCompraId,
                request.RecebedorId,
                request.DataVencimento,
                request.FormaPagamentoId,
                request.CartaoId,
                request.ContaBancariaId,
                request.ValorOriginal,
                request.ValorDesconto,
                request.ValorJuros,
                request.ValorMulta,
                request.Descricao,
                request.Observacao,
                StatusConta.PendenteId,
                ConverterRateios(request.Rateios));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw ConverterParaValidacao(exception);
        }
    }

    private async Task SincronizarRateiosContaAsync(ContaPagar conta, CancellationToken cancellationToken)
    {
        var rateiosExistentes = await dbContext.RateiosContaGerencial
            .Where(x => x.ContaPagarId == conta.Id)
            .ToListAsync(cancellationToken);

        dbContext.RateiosContaGerencial.RemoveRange(rateiosExistentes);
        dbContext.RateiosContaGerencial.AddRange(conta.Rateios);
    }

    private async Task SincronizarMovimentacaoEconomicaAsync(
        ContaPagar conta,
        bool compraCartao,
        CancellationToken cancellationToken)
    {
        var movimentoEconomico = await dbContext.MovimentacoesFinanceiras
            .SingleOrDefaultAsync(
                x => x.ContaPagarId == conta.Id && x.Natureza == NaturezaMovimentacao.Economica,
                cancellationToken);

        if (compraCartao)
        {
            if (movimentoEconomico is null)
            {
                dbContext.MovimentacoesFinanceiras.Add(CriarMovimentacaoEconomicaCompraCartao(conta));
            }
            else
            {
                movimentoEconomico.AtualizarEconomicaContaPagar(
                    conta.DataEmissao,
                    conta.ValorLiquido,
                    StatusMovimentacao.EfetivadaId,
                    conta.Descricao);
            }
        }
        else if (movimentoEconomico is not null)
        {
            movimentoEconomico.Cancelar(StatusMovimentacao.CanceladaId);
        }
    }

    private async Task<RegraRecorrencia> ObterRegraRecorrenciaObrigatoriaAsync(
        ContaPagar conta,
        CancellationToken cancellationToken)
    {
        if (!conta.RegraRecorrenciaId.HasValue)
        {
            throw ValidationExceptionFactory.Create("Recorrencia", "A conta informada nao possui regra de recorrencia.");
        }

        return await dbContext.RegrasRecorrencia
            .SingleAsync(x => x.Id == conta.RegraRecorrenciaId.Value, cancellationToken);
    }

    private static RecorrenciaResponse? MapearRecorrencia(RegraRecorrencia? regra)
    {
        return regra is null
            ? null
            : new RecorrenciaResponse(
                regra.Id,
                MapearTipoPeriodicidadeContrato(regra.TipoPeriodicidade),
                regra.DiaGeracaoMensal,
                regra.DataInicio,
                regra.DataFim,
                regra.Ativa,
                regra.PermiteEdicaoOcorrenciaIndividual,
                regra.Observacao);
    }

    private sealed record ContaPagarValidationContext(bool LiquidarNaCriacao, bool CompraCartao);

    private sealed record ContaPagarRecorrenciaTemplate(
        string? NumeroDocumento,
        DateOnly DataEmissao,
        Guid? ResponsavelCompraId,
        Guid RecebedorId,
        DateOnly DataVencimento,
        Guid FormaPagamentoId,
        Guid? CartaoId,
        Guid? ContaBancariaId,
        decimal ValorOriginal,
        decimal ValorDesconto,
        decimal ValorJuros,
        decimal ValorMulta,
        string Descricao,
        string? Observacao,
        IReadOnlyCollection<RateioRecorrenciaTemplate> Rateios);

    private sealed record RateioRecorrenciaTemplate(Guid ContaGerencialId, decimal Valor);
}
