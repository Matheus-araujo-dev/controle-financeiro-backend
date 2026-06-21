using System.Text.RegularExpressions;
using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Financeiro;

public sealed class ContaPagar : TenantEntity
{
    private static readonly Regex ParcelaDescricaoRegex = new(
        @"(?<!\d)(?<atual>\d{1,2})\s*/\s*(?<total>\d{1,2})(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly List<RateioContaGerencial> _rateios = [];

    private ContaPagar()
    {
    }

    public string? NumeroDocumento { get; private set; }

    public DateOnly DataEmissao { get; private set; }

    public Guid? ResponsavelCompraId { get; private set; }

    public Guid RecebedorId { get; private set; }

    public DateOnly DataVencimento { get; private set; }

    public DateOnly? DataLiquidacao { get; private set; }

    public Guid FormaPagamentoId { get; private set; }

    public Guid? CartaoId { get; private set; }

    public Guid? ContaBancariaId { get; private set; }

    public decimal ValorOriginal { get; private set; }

    public decimal ValorDesconto { get; private set; }

    public decimal ValorJuros { get; private set; }

    public decimal ValorMulta { get; private set; }

    public decimal ValorLiquido { get; private set; }

    public int QuantidadeParcelas { get; private set; }

    public int NumeroParcela { get; private set; }

    public Guid? GrupoParcelamentoId { get; private set; }

    public Guid? OrigemCompraPlanejadaId { get; private set; }

    public Guid? OrigemImportacaoWhatsappId { get; private set; }

    public Guid? FaturaCartaoId { get; private set; }

    public string? ChaveSerieImportacaoCartao { get; private set; }

    public string Descricao { get; private set; } = string.Empty;

    public string? Observacao { get; private set; }

    public Guid StatusContaId { get; private set; }

    public bool EhRecorrente { get; private set; }

    public Guid? RegraRecorrenciaId { get; private set; }

    public OrigemLancamento Origem { get; private set; }

    public IReadOnlyCollection<RateioContaGerencial> Rateios => _rateios;

    public static ContaPagar Criar(
        string? numeroDocumento,
        DateOnly dataEmissao,
        Guid? responsavelCompraId,
        Guid recebedorId,
        DateOnly dataVencimento,
        Guid formaPagamentoId,
        Guid? cartaoId,
        Guid? contaBancariaId,
        decimal valorOriginal,
        decimal valorDesconto,
        decimal valorJuros,
        decimal valorMulta,
        int quantidadeParcelas,
        int numeroParcela,
        Guid? grupoParcelamentoId,
        Guid? origemCompraPlanejadaId,
        string descricao,
        string? observacao,
        Guid statusContaId,
        bool ehRecorrente,
        Guid? regraRecorrenciaId,
        OrigemLancamento origem,
        IReadOnlyCollection<RateioPlano> rateios)
    {
        var conta = new ContaPagar();
        conta.DefinirCampos(
            numeroDocumento,
            dataEmissao,
            responsavelCompraId,
            recebedorId,
            dataVencimento,
            formaPagamentoId,
            cartaoId,
            contaBancariaId,
            valorOriginal,
            valorDesconto,
            valorJuros,
            valorMulta,
            quantidadeParcelas,
            numeroParcela,
            grupoParcelamentoId,
            origemCompraPlanejadaId,
            descricao,
            observacao,
            statusContaId,
            ehRecorrente,
            regraRecorrenciaId,
            origem);
        conta.SubstituirRateios(rateios);
        return conta;
    }

    public static IReadOnlyCollection<ContaPagar> CriarParcelas(
        string? numeroDocumento,
        DateOnly dataEmissao,
        Guid? responsavelCompraId,
        Guid recebedorId,
        DateOnly dataVencimento,
        Guid formaPagamentoId,
        Guid? cartaoId,
        Guid? contaBancariaId,
        decimal valorOriginal,
        decimal valorDesconto,
        decimal valorJuros,
        decimal valorMulta,
        int quantidadeParcelas,
        Guid? origemCompraPlanejadaId,
        string descricao,
        string? observacao,
        Guid statusContaId,
        bool ehRecorrente,
        Guid? regraRecorrenciaId,
        OrigemLancamento origem,
        IReadOnlyCollection<RateioPlano> rateios)
    {
        if (quantidadeParcelas <= 1)
        {
            return
            [
                Criar(
                    numeroDocumento,
                    dataEmissao,
                    responsavelCompraId,
                    recebedorId,
                    dataVencimento,
                    formaPagamentoId,
                    cartaoId,
                    contaBancariaId,
                    valorOriginal,
                    valorDesconto,
                    valorJuros,
                    valorMulta,
                    1,
                    1,
                    null,
                    origemCompraPlanejadaId,
                    descricao,
                    observacao,
                    statusContaId,
                    ehRecorrente,
                    regraRecorrenciaId,
                    origem,
                    rateios)
            ];
        }

        var valorLiquidoTotal = CalcularValorLiquido(valorOriginal, valorDesconto, valorJuros, valorMulta);
        ValidarRateios(rateios, valorLiquidoTotal);

        var valorOriginalParcelado = ParcelamentoHelper.Distribuir(valorOriginal, quantidadeParcelas).ToArray();
        var valorDescontoParcelado = ParcelamentoHelper.Distribuir(valorDesconto, quantidadeParcelas).ToArray();
        var valorJurosParcelado = ParcelamentoHelper.Distribuir(valorJuros, quantidadeParcelas).ToArray();
        var valorMultaParcelado = ParcelamentoHelper.Distribuir(valorMulta, quantidadeParcelas).ToArray();
        var valorLiquidoParcelado = ParcelamentoHelper.Distribuir(valorLiquidoTotal, quantidadeParcelas).ToArray();

        var grupoParcelamentoId = Guid.NewGuid();
        var parcelas = new List<ContaPagar>(quantidadeParcelas);

        for (var index = 0; index < quantidadeParcelas; index++)
        {
            var valorParcela = valorLiquidoParcelado[index];
            var rateiosParcela = ParcelamentoHelper.DistribuirRateios(rateios, valorParcela, valorLiquidoTotal);

            parcelas.Add(Criar(
                numeroDocumento,
                dataEmissao,
                responsavelCompraId,
                recebedorId,
                dataVencimento.AddMonths(index),
                formaPagamentoId,
                cartaoId,
                contaBancariaId,
                valorOriginalParcelado[index],
                valorDescontoParcelado[index],
                valorJurosParcelado[index],
                valorMultaParcelado[index],
                quantidadeParcelas,
                index + 1,
                grupoParcelamentoId,
                origemCompraPlanejadaId,
                descricao,
                observacao,
                statusContaId,
                ehRecorrente,
                regraRecorrenciaId,
                origem,
                rateiosParcela));
        }

        return parcelas;
    }

    public static IReadOnlyCollection<ContaPagar> CriarParcelasCartao(
        string? numeroDocumento,
        DateOnly dataEmissao,
        Guid? responsavelCompraId,
        Guid recebedorId,
        Guid formaPagamentoId,
        Guid cartaoId,
        decimal valorOriginal,
        decimal valorDesconto,
        decimal valorJuros,
        decimal valorMulta,
        int quantidadeParcelas,
        Guid? origemCompraPlanejadaId,
        string descricao,
        string? observacao,
        Guid statusContaId,
        bool ehRecorrente,
        Guid? regraRecorrenciaId,
        OrigemLancamento origem,
        IReadOnlyCollection<RateioPlano> rateios,
        int diaFechamentoFatura,
        int diaVencimentoFatura)
    {
        var valorLiquidoTotal = CalcularValorLiquido(valorOriginal, valorDesconto, valorJuros, valorMulta);
        ValidarRateios(rateios, valorLiquidoTotal);

        if (quantidadeParcelas <= 1)
        {
            var competenciaPrimeiraParcela = FaturaCartaoCompetencia.Calcular(dataEmissao, diaFechamentoFatura, diaVencimentoFatura);
            return
            [
                Criar(
                    numeroDocumento,
                    dataEmissao,
                    responsavelCompraId,
                    recebedorId,
                    competenciaPrimeiraParcela.DataVencimento,
                    formaPagamentoId,
                    cartaoId,
                    null,
                    valorOriginal,
                    valorDesconto,
                    valorJuros,
                    valorMulta,
                    1,
                    1,
                    null,
                    origemCompraPlanejadaId,
                    descricao,
                    observacao,
                    statusContaId,
                    ehRecorrente,
                    regraRecorrenciaId,
                    origem,
                    rateios)
            ];
        }

        var valorOriginalParcelado = ParcelamentoHelper.Distribuir(valorOriginal, quantidadeParcelas).ToArray();
        var valorDescontoParcelado = ParcelamentoHelper.Distribuir(valorDesconto, quantidadeParcelas).ToArray();
        var valorJurosParcelado = ParcelamentoHelper.Distribuir(valorJuros, quantidadeParcelas).ToArray();
        var valorMultaParcelado = ParcelamentoHelper.Distribuir(valorMulta, quantidadeParcelas).ToArray();
        var valorLiquidoParcelado = ParcelamentoHelper.Distribuir(valorLiquidoTotal, quantidadeParcelas).ToArray();

        var grupoParcelamentoId = Guid.NewGuid();
        var parcelas = new List<ContaPagar>(quantidadeParcelas);

        for (var index = 0; index < quantidadeParcelas; index++)
        {
            var dataEmissaoParcela = dataEmissao.AddMonths(index);
            var competenciaParcela = FaturaCartaoCompetencia.Calcular(dataEmissaoParcela, diaFechamentoFatura, diaVencimentoFatura);
            var valorParcela = valorLiquidoParcelado[index];
            var rateiosParcela = ParcelamentoHelper.DistribuirRateios(rateios, valorParcela, valorLiquidoTotal);
            var descricaoParcela = AtualizarMarcadorParcelaDescricao(descricao, index + 1, quantidadeParcelas);

            parcelas.Add(Criar(
                numeroDocumento,
                dataEmissaoParcela,
                responsavelCompraId,
                recebedorId,
                competenciaParcela.DataVencimento,
                formaPagamentoId,
                cartaoId,
                null,
                valorOriginalParcelado[index],
                valorDescontoParcelado[index],
                valorJurosParcelado[index],
                valorMultaParcelado[index],
                quantidadeParcelas,
                index + 1,
                grupoParcelamentoId,
                origemCompraPlanejadaId,
                descricaoParcela,
                observacao,
                statusContaId,
                ehRecorrente,
                regraRecorrenciaId,
                origem,
                rateiosParcela));
        }

        return parcelas;
    }

    private static string AtualizarMarcadorParcelaDescricao(string descricao, int numeroParcela, int quantidadeParcelas)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            return descricao;
        }

        return ParcelaDescricaoRegex.Replace(descricao, $"{numeroParcela}/{quantidadeParcelas}", 1);
    }

    public void Atualizar(
        string? numeroDocumento,
        DateOnly dataEmissao,
        Guid? responsavelCompraId,
        Guid recebedorId,
        DateOnly dataVencimento,
        Guid formaPagamentoId,
        Guid? cartaoId,
        Guid? contaBancariaId,
        decimal valorOriginal,
        decimal valorDesconto,
        decimal valorJuros,
        decimal valorMulta,
        string descricao,
        string? observacao,
        Guid statusContaId,
        IReadOnlyCollection<RateioPlano> rateios)
    {
        if (StatusContaId == StatusConta.LiquidadaId || StatusContaId == StatusConta.CanceladaId)
        {
            throw new InvalidOperationException("Não é permitido editar contas liquidadas ou canceladas.");
        }

        DefinirCampos(
            numeroDocumento,
            dataEmissao,
            responsavelCompraId,
            recebedorId,
            dataVencimento,
            formaPagamentoId,
            cartaoId,
            contaBancariaId,
            valorOriginal,
            valorDesconto,
            valorJuros,
            valorMulta,
            QuantidadeParcelas,
            NumeroParcela,
            GrupoParcelamentoId,
            OrigemCompraPlanejadaId,
            descricao,
            observacao,
            statusContaId,
            EhRecorrente,
            RegraRecorrenciaId,
            Origem);
        SubstituirRateios(rateios);
    }

    public void VincularRecorrencia(Guid regraRecorrenciaId)
    {
        if (StatusContaId == StatusConta.LiquidadaId || StatusContaId == StatusConta.CanceladaId)
        {
            throw new InvalidOperationException("NÃ£o Ã© permitido editar contas liquidadas ou canceladas.");
        }

        if (regraRecorrenciaId == Guid.Empty)
        {
            throw new ArgumentException("Regra de recorrÃªncia Ã© obrigatÃ³ria.", nameof(regraRecorrenciaId));
        }

        EhRecorrente = true;
        RegraRecorrenciaId = regraRecorrenciaId;
    }

    public void Liquidar(DateOnly dataLiquidacao, Guid contaBancariaId, Guid statusContaLiquidadaId)
    {
        if (StatusContaId == StatusConta.CanceladaId)
        {
            throw new InvalidOperationException("Conta cancelada não pode ser liquidada.");
        }

        DataLiquidacao = dataLiquidacao;
        ContaBancariaId = contaBancariaId;
        StatusContaId = statusContaLiquidadaId;
    }

    public void AtualizarValorLiquido(decimal novoValorLiquido, IReadOnlyCollection<RateioPlano> rateios)
    {
        if (StatusContaId == StatusConta.LiquidadaId || StatusContaId == StatusConta.CanceladaId)
        {
            throw new InvalidOperationException("Não é permitido editar contas liquidadas ou canceladas.");
        }

        if (novoValorLiquido <= 0)
        {
            throw new ArgumentException("Valor líquido deve ser maior que zero.", nameof(novoValorLiquido));
        }

        var novoValorOriginal = decimal.Round(novoValorLiquido + ValorDesconto - ValorJuros - ValorMulta, 2, MidpointRounding.AwayFromZero);
        if (novoValorOriginal <= 0)
        {
            throw new ArgumentException("Valor original deve ser maior que zero.", nameof(novoValorLiquido));
        }

        ValorOriginal = novoValorOriginal;
        ValorLiquido = decimal.Round(novoValorLiquido, 2, MidpointRounding.AwayFromZero);
        SubstituirRateios(rateios);
    }

    public void Estornar(Guid statusContaPendenteId)
    {
        if (StatusContaId != StatusConta.LiquidadaId)
        {
            throw new InvalidOperationException("Apenas contas liquidadas podem ser estornadas.");
        }

        StatusContaId = statusContaPendenteId;
        DataLiquidacao = null;
        ContaBancariaId = null;
    }

    public void Cancelar(Guid statusContaCanceladaId)
    {
        if (StatusContaId == StatusConta.LiquidadaId)
        {
            throw new InvalidOperationException("Conta liquidada não pode ser cancelada.");
        }

        StatusContaId = statusContaCanceladaId;
    }

    public void VincularOrigemImportacao(Guid importacaoWhatsappId)
    {
        if (importacaoWhatsappId == Guid.Empty)
        {
            throw new ArgumentException("Importação é obrigatória.", nameof(importacaoWhatsappId));
        }

        OrigemImportacaoWhatsappId = importacaoWhatsappId;
    }

    public void VincularFaturaCartao(Guid faturaCartaoId)
    {
        if (faturaCartaoId == Guid.Empty)
        {
            throw new ArgumentException("Fatura é obrigatória.", nameof(faturaCartaoId));
        }

        FaturaCartaoId = faturaCartaoId;
    }

    public void DefinirChaveSerieImportacaoCartao(string? chaveSerie)
    {
        ChaveSerieImportacaoCartao = string.IsNullOrWhiteSpace(chaveSerie) ? null : chaveSerie.Trim();
    }

    private void DefinirCampos(
        string? numeroDocumento,
        DateOnly dataEmissao,
        Guid? responsavelCompraId,
        Guid recebedorId,
        DateOnly dataVencimento,
        Guid formaPagamentoId,
        Guid? cartaoId,
        Guid? contaBancariaId,
        decimal valorOriginal,
        decimal valorDesconto,
        decimal valorJuros,
        decimal valorMulta,
        int quantidadeParcelas,
        int numeroParcela,
        Guid? grupoParcelamentoId,
        Guid? origemCompraPlanejadaId,
        string descricao,
        string? observacao,
        Guid statusContaId,
        bool ehRecorrente,
        Guid? regraRecorrenciaId,
        OrigemLancamento origem)
    {
        if (recebedorId == Guid.Empty)
        {
            throw new ArgumentException("Recebedor é obrigatório.", nameof(recebedorId));
        }

        if (formaPagamentoId == Guid.Empty)
        {
            throw new ArgumentException("Forma de pagamento é obrigatória.", nameof(formaPagamentoId));
        }

        if (string.IsNullOrWhiteSpace(descricao))
        {
            throw new ArgumentException("Descrição é obrigatória.", nameof(descricao));
        }

        if (quantidadeParcelas < 1)
        {
            throw new ArgumentException("Quantidade de parcelas inválida.", nameof(quantidadeParcelas));
        }

        if (numeroParcela < 1 || numeroParcela > quantidadeParcelas)
        {
            throw new ArgumentException("Número da parcela inválido.", nameof(numeroParcela));
        }

        NumeroDocumento = string.IsNullOrWhiteSpace(numeroDocumento) ? null : numeroDocumento.Trim();
        DataEmissao = dataEmissao;
        ResponsavelCompraId = responsavelCompraId;
        RecebedorId = recebedorId;
        DataVencimento = dataVencimento;
        FormaPagamentoId = formaPagamentoId;
        CartaoId = cartaoId;
        ContaBancariaId = contaBancariaId;
        ValorOriginal = decimal.Round(valorOriginal, 2, MidpointRounding.AwayFromZero);
        ValorDesconto = decimal.Round(valorDesconto, 2, MidpointRounding.AwayFromZero);
        ValorJuros = decimal.Round(valorJuros, 2, MidpointRounding.AwayFromZero);
        ValorMulta = decimal.Round(valorMulta, 2, MidpointRounding.AwayFromZero);
        ValorLiquido = CalcularValorLiquido(valorOriginal, valorDesconto, valorJuros, valorMulta);
        QuantidadeParcelas = quantidadeParcelas;
        NumeroParcela = numeroParcela;
        GrupoParcelamentoId = grupoParcelamentoId;
        OrigemCompraPlanejadaId = origemCompraPlanejadaId;
        Descricao = descricao.Trim();
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        // Compra de cartão não é uma conta em aberto: fica "Em fatura" e será
        // liquidada em massa quando a fatura do cartão for paga (e vice-versa
        // quando o cartão é removido da conta).
        StatusContaId = statusContaId switch
        {
            _ when cartaoId.HasValue && statusContaId == StatusConta.PendenteId => StatusConta.EmFaturaId,
            _ when !cartaoId.HasValue && statusContaId == StatusConta.EmFaturaId => StatusConta.PendenteId,
            _ => statusContaId
        };
        EhRecorrente = ehRecorrente;
        RegraRecorrenciaId = regraRecorrenciaId;
        Origem = origem;

        if (ValorLiquido == 0)
        {
            throw new ArgumentException("Valor liquido deve ser diferente de zero.", nameof(valorOriginal));
        }

        if (ValorLiquido < 0 && !CartaoId.HasValue)
        {
            throw new ArgumentException("Valor liquido deve ser maior que zero para contas operacionais.", nameof(valorOriginal));
        }
    }

    private void SubstituirRateios(IReadOnlyCollection<RateioPlano> rateios)
    {
        ValidarRateios(rateios, ValorLiquido);
        _rateios.Clear();
        _rateios.AddRange(rateios.Select(rateio => RateioContaGerencial.CriarContaPagar(Id, rateio, ValorLiquido)));
    }

    private static void ValidarRateios(IReadOnlyCollection<RateioPlano> rateios, decimal valorLiquido)
    {
        if (rateios.Count == 0)
        {
            throw new ArgumentException("Ao menos um rateio é obrigatório.", nameof(rateios));
        }

        var totalRateio = rateios.Sum(x => x.Valor);

        if (totalRateio != valorLiquido)
        {
            throw new ArgumentException("A soma dos rateios deve fechar exatamente o valor líquido.", nameof(rateios));
        }
    }

    private static decimal CalcularValorLiquido(decimal valorOriginal, decimal valorDesconto, decimal valorJuros, decimal valorMulta)
    {
        return decimal.Round(valorOriginal - valorDesconto + valorJuros + valorMulta, 2, MidpointRounding.AwayFromZero);
    }
}
