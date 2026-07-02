using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.PlanejamentoCompras;

public enum PrioridadePlanejamentoCompra
{
    Baixa = 1,
    Media = 2,
    Alta = 3
}

public enum StatusPlanejamentoCompra
{
    Planejada = 1,
    Comprada = 2,
    Cancelada = 3
}

public sealed class PlanejamentoCompra : TenantEntity
{
    private PlanejamentoCompra()
    {
    }

    public string Titulo { get; private set; } = string.Empty;

    public string? Descricao { get; private set; }

    public decimal ValorEstimado { get; private set; }

    public DateOnly? DataDesejada { get; private set; }

    public PrioridadePlanejamentoCompra Prioridade { get; private set; }

    public StatusPlanejamentoCompra Status { get; private set; }

    public bool Parcelavel { get; private set; }

    public int? QuantidadeParcelasDesejada { get; private set; }

    public Guid ContaGerencialId { get; private set; }

    public Guid ResponsavelId { get; private set; }

    public string? Link { get; private set; }

    public string? Observacao { get; private set; }

    public Guid? ContaPagarGeradaId { get; private set; }

    public DateTime? ConvertidaEmContaPagarEmUtc { get; private set; }

    public static PlanejamentoCompra Criar(
        string titulo,
        string? descricao,
        decimal valorEstimado,
        DateOnly? dataDesejada,
        PrioridadePlanejamentoCompra prioridade,
        StatusPlanejamentoCompra status,
        bool parcelavel,
        int? quantidadeParcelasDesejada,
        Guid contaGerencialId,
        Guid responsavelId,
        string? link,
        string? observacao)
    {
        var planejamento = new PlanejamentoCompra();
        planejamento.Atualizar(
            titulo,
            descricao,
            valorEstimado,
            dataDesejada,
            prioridade,
            status,
            parcelavel,
            quantidadeParcelasDesejada,
            contaGerencialId,
            responsavelId,
            link,
            observacao);

        return planejamento;
    }

    public void Atualizar(
        string titulo,
        string? descricao,
        decimal valorEstimado,
        DateOnly? dataDesejada,
        PrioridadePlanejamentoCompra prioridade,
        StatusPlanejamentoCompra status,
        bool parcelavel,
        int? quantidadeParcelasDesejada,
        Guid contaGerencialId,
        Guid responsavelId,
        string? link,
        string? observacao)
    {
        if (string.IsNullOrWhiteSpace(titulo))
        {
            throw new ArgumentException("Título é obrigatório.", nameof(titulo));
        }

        if (valorEstimado <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valorEstimado), "Valor estimado deve ser maior que zero.");
        }

        if (contaGerencialId == Guid.Empty)
        {
            throw new ArgumentException("Conta gerencial é obrigatória.", nameof(contaGerencialId));
        }

        if (responsavelId == Guid.Empty)
        {
            throw new ArgumentException("Responsável é obrigatório.", nameof(responsavelId));
        }

        if (!string.IsNullOrWhiteSpace(link) &&
            (!Uri.TryCreate(link.Trim(), UriKind.Absolute, out var parsedLink) || string.IsNullOrWhiteSpace(parsedLink.Scheme)))
        {
            throw new ArgumentException("Link inválido. Informe uma URL absoluta.", nameof(link));
        }

        if (quantidadeParcelasDesejada.HasValue && quantidadeParcelasDesejada.Value < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidadeParcelasDesejada), "Quantidade de parcelas desejada deve ser maior ou igual a 2.");
        }

        Titulo = titulo.Trim();
        Descricao = NormalizarOpcional(descricao);
        ValorEstimado = decimal.Round(valorEstimado, 2, MidpointRounding.AwayFromZero);
        DataDesejada = dataDesejada;
        Prioridade = prioridade;
        Status = status;
        Parcelavel = parcelavel;
        QuantidadeParcelasDesejada = parcelavel ? quantidadeParcelasDesejada : null;
        ContaGerencialId = contaGerencialId;
        ResponsavelId = responsavelId;
        Link = NormalizarOpcional(link);
        Observacao = NormalizarOpcional(observacao);
    }

    public void MarcarComoConvertidaEmContaPagar(Guid contaPagarId)
    {
        if (contaPagarId == Guid.Empty)
        {
            throw new ArgumentException("Conta a pagar gerada e obrigatoria.", nameof(contaPagarId));
        }

        if (ContaPagarGeradaId.HasValue)
        {
            throw new InvalidOperationException("Compra planejada ja foi convertida em conta a pagar.");
        }

        ContaPagarGeradaId = contaPagarId;
        ConvertidaEmContaPagarEmUtc = DateTime.UtcNow;
        Status = StatusPlanejamentoCompra.Comprada;
    }

    public void MarcarComoComprada()
    {
        if (Status == StatusPlanejamentoCompra.Comprada)
        {
            throw new InvalidOperationException("Compra planejada ja foi realizada.");
        }

        Status = StatusPlanejamentoCompra.Comprada;
    }

    public void ReverterParaPlanejada()
    {
        Status = StatusPlanejamentoCompra.Planejada;
        ContaPagarGeradaId = null;
        ConvertidaEmContaPagarEmUtc = null;
    }

    public void CancelarPlanejamento()
    {
        Status = StatusPlanejamentoCompra.Cancelada;
        ContaPagarGeradaId = null;
        ConvertidaEmContaPagarEmUtc = null;
    }

    private static string? NormalizarOpcional(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }
}
