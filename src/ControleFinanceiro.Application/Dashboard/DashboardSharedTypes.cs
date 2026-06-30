using ControleFinanceiro.Application.ImportacoesWhatsapp;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;

namespace ControleFinanceiro.Application.Dashboard;

internal sealed record StatusContaInfo(string Codigo, string Nome);

internal sealed record ContaGerencialInfo(string? Codigo, string Descricao, Domain.Cadastros.ContasGerenciais.TipoContaGerencial Tipo);

internal sealed record ContaJanelaInfo(
    Guid Id,
    string TipoLancamento,
    string Descricao,
    DateOnly DataVencimento,
    decimal ValorLiquido,
    Guid StatusContaId,
    Guid PessoaId,
    Guid? ResponsavelId,
    Guid? RegraRecorrenciaId,
    OrigemLancamento Origem);

internal sealed record ContaRecorrenciaInfo(
    Guid Id,
    Guid RegraId,
    DateOnly DataVencimento,
    decimal ValorLiquido,
    string Descricao,
    OrigemLancamento Origem,
    Guid PessoaId,
    Guid? ResponsavelId);

internal sealed record RecorrenciaProjetada(
    Guid RegraId,
    DateOnly Data,
    TipoMovimentacao Tipo,
    decimal Valor,
    string Descricao,
    Guid PessoaId,
    Guid? ResponsavelId,
    Guid ContaTemplateId,
    bool EhContaPagar);

internal sealed record ImportacaoCompraInfo(
    Guid Id,
    string Descricao,
    decimal Valor,
    DateOnly DataCompra,
    DateOnly DataVencimento,
    TipoMovimentacao Tipo,
    bool Recorrente,
    ParcelamentoCompraCartaoInfo? Parcelamento,
    string? SerieRecorrenteKey,
    string? SerieParcelamentoKey,
    Guid? ContaGerencialId,
    Guid? ResponsavelId);

internal sealed record RateioLancamentoInfo(
    Guid LancamentoId,
    string TipoLancamento,
    string Descricao,
    Guid PessoaId,
    DateOnly DataEmissao,
    DateOnly DataVencimento,
    decimal ValorLancamento,
    decimal ValorRateio,
    Guid StatusContaId,
    Guid ContaGerencialId);

internal sealed record PrevisaoItem(
    string TipoReferencia,
    Guid ReferenciaId,
    DateOnly Data,
    TipoMovimentacao Tipo,
    Contracts.Dashboard.DashboardCentralPrevisaoOrigem Origem,
    Contracts.Dashboard.DashboardCentralPrevisaoStatus Status,
    string Descricao,
    decimal Valor,
    string? PessoaNome,
    string? ResponsavelNome,
    Guid? ContaGerencialId);
