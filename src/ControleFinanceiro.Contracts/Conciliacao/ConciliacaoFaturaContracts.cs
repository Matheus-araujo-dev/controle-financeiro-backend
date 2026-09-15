using System.Text.Json;
using ControleFinanceiro.Contracts.Financeiro.Common;

namespace ControleFinanceiro.Contracts.Conciliacao;

public sealed record VincularItemFaturaRequest(Guid ContaPagarId, decimal ValorEsperadoSistema, bool UsarValorFatura, bool Aprender = true, IReadOnlyList<string>? CamposParaAprender = null, ReembolsoFaturaConfig? Reembolso = null);
public sealed record RateioConciliacaoResponse(Guid ContaGerencialId, decimal Valor);
public sealed record ContaConciliacaoResponse(Guid Id, DateOnly DataCompra, string Descricao, decimal Valor,
    int NumeroParcela, int QuantidadeParcelas, Guid? ResponsavelCompraId, Guid RecebedorId,
    Guid FormaPagamentoId, Guid? RegraRecorrenciaId, Guid? GrupoReembolsoId,
    IReadOnlyList<RateioConciliacaoResponse> Rateios);
public sealed record CandidatoConciliacaoResponse(Guid ContaId, decimal Diferenca, int Pontos, string Motivo, bool CorrespondenciaClara);
public sealed record ItemFaturaConciliacaoResponse(Guid Id, DateOnly Data, string DescricaoOriginal, decimal Valor,
    int NumeroParcela, int QuantidadeParcelas, string Status, Guid? ContaPagarVinculadaId,
    decimal? ValorAnteriorSistema, IReadOnlyList<CandidatoConciliacaoResponse> Candidatos, PreferenciasFaturaResponse? Preferencias = null, JsonElement? Rascunho = null, DateTime AtualizadoEmUtc = default);
public sealed record ConciliacaoFaturaResponse(Guid Id, Guid FaturaId, string NomeArquivo, string Status,
    IReadOnlyList<ItemFaturaConciliacaoResponse> Itens, IReadOnlyList<ContaConciliacaoResponse> ContasSistema);

public sealed record ReembolsoFaturaConfig(bool ParcelarIgual, decimal ValorTotal, IReadOnlyList<Guid> PagadoresIds,
    Guid FormaPagamentoId, DateOnly DataVencimento, string Descricao, string? Observacao, IReadOnlyList<RateioRequest> Rateios);
public sealed record CriarItemFaturaRequest(string Descricao, Guid RecebedorId, Guid? ResponsavelCompraId,
    Guid FormaPagamentoId, IReadOnlyList<RateioRequest> Rateios, RecorrenciaConfigRequest? Recorrencia,
    ReembolsoFaturaConfig? Reembolso, string? Observacao, bool Aprender = true, IReadOnlyList<string>? CamposParaAprender = null);

public sealed record RateioMemoriaResponse(Guid ContaGerencialId, decimal Proporcao);
public sealed record PreferenciasFaturaResponse(string? Descricao, Guid? ResponsavelCompraId, Guid? RecebedorId,
    IReadOnlyList<RateioMemoriaResponse>? Rateios, bool? EhRecorrente, bool? GerarReembolso, IReadOnlyList<Guid>? ReembolsoPagadoresIds);

public sealed record RascunhoFaturaRequest(JsonElement Dados, DateTime AtualizadoEmUtc);
public sealed record RascunhoFaturaResponse(DateTime AtualizadoEmUtc);
public sealed record RevisaoFaturaResumoResponse(Guid Id, string NomeArquivo, string Status, DateTime CriadoEmUtc);

public sealed record PreviaReembolsoFaturaRequest(Guid? ContaPagarId, bool ParcelarIgual, decimal ValorTotal,
    IReadOnlyList<Guid> PagadoresIds, DateOnly DataVencimento);
public sealed record PreviaReembolsoFaturaResponse(Guid PagadorId, string PagadorNome, int NumeroParcela,
    int QuantidadeParcelas, decimal Valor, DateOnly DataVencimento);
