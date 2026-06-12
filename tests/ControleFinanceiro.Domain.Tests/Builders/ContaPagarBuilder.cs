using ControleFinanceiro.Domain.Financeiro;

namespace ControleFinanceiro.Domain.Tests.Builders;

public sealed class ContaPagarBuilder
{
    private string? _numeroDocumento = "DOC-001";
    private DateOnly _dataEmissao = new(2026, 4, 1);
    private Guid? _responsavelCompraId;
    private Guid _recebedorId = Guid.NewGuid();
    private DateOnly _dataVencimento = new(2026, 4, 10);
    private Guid _formaPagamentoId = Guid.NewGuid();
    private Guid? _cartaoId;
    private Guid? _contaBancariaId;
    private decimal _valorOriginal = 100m;
    private decimal _valorDesconto;
    private decimal _valorJuros;
    private decimal _valorMulta;
    private int _quantidadeParcelas = 1;
    private int _numeroParcela = 1;
    private Guid? _grupoParcelamentoId;
    private Guid? _origemCompraPlanejadaId;
    private string _descricao = "Conta teste";
    private string? _observacao;
    private Guid _statusContaId = StatusConta.PendenteId;
    private bool _ehRecorrente;
    private Guid? _regraRecorrenciaId;
    private OrigemLancamento _origem = OrigemLancamento.Manual;
    private List<RateioPlano> _rateios = [];

    public ContaPagarBuilder ComNumeroDocumento(string? numeroDocumento)
    {
        _numeroDocumento = numeroDocumento;
        return this;
    }

    public ContaPagarBuilder ComDataEmissao(DateOnly dataEmissao)
    {
        _dataEmissao = dataEmissao;
        return this;
    }

    public ContaPagarBuilder ComResponsavelCompraId(Guid? responsavelCompraId)
    {
        _responsavelCompraId = responsavelCompraId;
        return this;
    }

    public ContaPagarBuilder ComRecebedorId(Guid recebedorId)
    {
        _recebedorId = recebedorId;
        return this;
    }

    public ContaPagarBuilder ComDataVencimento(DateOnly dataVencimento)
    {
        _dataVencimento = dataVencimento;
        return this;
    }

    public ContaPagarBuilder ComFormaPagamentoId(Guid formaPagamentoId)
    {
        _formaPagamentoId = formaPagamentoId;
        return this;
    }

    public ContaPagarBuilder ComCartaoId(Guid? cartaoId)
    {
        _cartaoId = cartaoId;
        return this;
    }

    public ContaPagarBuilder ComContaBancariaId(Guid? contaBancariaId)
    {
        _contaBancariaId = contaBancariaId;
        return this;
    }

    public ContaPagarBuilder ComValorOriginal(decimal valorOriginal)
    {
        _valorOriginal = valorOriginal;
        return this;
    }

    public ContaPagarBuilder ComValorDesconto(decimal valorDesconto)
    {
        _valorDesconto = valorDesconto;
        return this;
    }

    public ContaPagarBuilder ComValorJuros(decimal valorJuros)
    {
        _valorJuros = valorJuros;
        return this;
    }

    public ContaPagarBuilder ComValorMulta(decimal valorMulta)
    {
        _valorMulta = valorMulta;
        return this;
    }

    public ContaPagarBuilder ComQuantidadeParcelas(int quantidadeParcelas)
    {
        _quantidadeParcelas = quantidadeParcelas;
        return this;
    }

    public ContaPagarBuilder ComNumeroParcela(int numeroParcela)
    {
        _numeroParcela = numeroParcela;
        return this;
    }

    public ContaPagarBuilder ComGrupoParcelamentoId(Guid? grupoParcelamentoId)
    {
        _grupoParcelamentoId = grupoParcelamentoId;
        return this;
    }

    public ContaPagarBuilder ComOrigemCompraPlanejadaId(Guid? origemCompraPlanejadaId)
    {
        _origemCompraPlanejadaId = origemCompraPlanejadaId;
        return this;
    }

    public ContaPagarBuilder ComDescricao(string descricao)
    {
        _descricao = descricao;
        return this;
    }

    public ContaPagarBuilder ComObservacao(string? observacao)
    {
        _observacao = observacao;
        return this;
    }

    public ContaPagarBuilder ComStatusContaId(Guid statusContaId)
    {
        _statusContaId = statusContaId;
        return this;
    }

    public ContaPagarBuilder Pendente()
    {
        _statusContaId = StatusConta.PendenteId;
        return this;
    }

    public ContaPagarBuilder Liquidada()
    {
        _statusContaId = StatusConta.LiquidadaId;
        return this;
    }

    public ContaPagarBuilder Cancelada()
    {
        _statusContaId = StatusConta.CanceladaId;
        return this;
    }

    public ContaPagarBuilder ComEhRecorrente(bool ehRecorrente)
    {
        _ehRecorrente = ehRecorrente;
        return this;
    }

    public ContaPagarBuilder ComRegraRecorrenciaId(Guid? regraRecorrenciaId)
    {
        _regraRecorrenciaId = regraRecorrenciaId;
        return this;
    }

    public ContaPagarBuilder ComOrigem(OrigemLancamento origem)
    {
        _origem = origem;
        return this;
    }

    public ContaPagarBuilder ComRateios(params RateioPlano[] rateios)
    {
        _rateios = [.. rateios];
        return this;
    }

    public ContaPagarBuilder ComRateio(Guid contaGerencialId, decimal valor)
    {
        _rateios.Add(RateioPlano.Create(contaGerencialId, valor));
        return this;
    }

    public ContaPagar Build()
    {
        if (_rateios.Count == 0)
        {
            var valorLiquido = _valorOriginal - _valorDesconto + _valorJuros + _valorMulta;
            _rateios.Add(RateioPlano.Create(Guid.NewGuid(), valorLiquido));
        }

        return ContaPagar.Criar(
            _numeroDocumento,
            _dataEmissao,
            _responsavelCompraId,
            _recebedorId,
            _dataVencimento,
            _formaPagamentoId,
            _cartaoId,
            _contaBancariaId,
            _valorOriginal,
            _valorDesconto,
            _valorJuros,
            _valorMulta,
            _quantidadeParcelas,
            _numeroParcela,
            _grupoParcelamentoId,
            _origemCompraPlanejadaId,
            _descricao,
            _observacao,
            _statusContaId,
            _ehRecorrente,
            _regraRecorrenciaId,
            _origem,
            _rateios);
    }
}