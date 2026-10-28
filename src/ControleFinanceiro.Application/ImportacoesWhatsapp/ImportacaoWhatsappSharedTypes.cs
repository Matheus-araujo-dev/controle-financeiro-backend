using ControleFinanceiro.Domain.Cadastros.Cartoes;
using ControleFinanceiro.Domain.Financeiro;
using ControleFinanceiro.Domain.ImportacoesWhatsapp;

namespace ControleFinanceiro.Application.ImportacoesWhatsapp;

internal sealed record ConfirmacaoClassificada(
    Guid? ContaReceberId,
    string? DescricaoAjustada,
    bool MarcarComoRecorrente);

internal sealed record AprovacaoFaturaContext(
    Guid RecebedorFaturaId,
    Guid PagadorFaturaId,
    Guid FormaPagamentoCartaoId,
    IReadOnlyDictionary<Guid, Cartao> Cartoes);

internal sealed record FaturaKey(Guid CartaoId, string Competencia);

internal sealed record ImportacaoMaterializadaResult(
    IReadOnlyCollection<ContaPagar> ContasGeradas,
    IReadOnlyCollection<FaturaKey> ChavesAfetadas,
    IReadOnlyCollection<FaturaKey> ChavesContaPagarFatura);

internal sealed record ItemImportadoPreparado(
    ItemImportadoWhatsapp Item,
    ImportacaoWhatsappSuggestionPayload Payload,
    Cartao Cartao,
    string Descricao,
    Guid ContaGerencialId,
    Guid PagadorId,
    decimal Valor);
