namespace ControleFinanceiro.Domain.Conciliacao;

public enum FormatoArquivo
{
    Ofx = 1,
    Csv = 2,
    Pdf = 3
}

public enum StatusConciliacao
{
    EmRevisao = 1,
    Concluida = 2,
    Cancelada = 3
}

public enum StatusItemConciliacao
{
    Pendente = 1,
    Conciliado = 2,
    Ignorado = 3
}