namespace ControleFinanceiro.Domain.ImportacoesWhatsapp;

public enum StatusImportacaoWhatsapp
{
    Recebido = 1,
    EmProcessamento = 2,
    ExtraidoComSucesso = 3,
    PendenteRevisao = 4,
    Confirmado = 5,
    Rejeitado = 6,
    ErroExtracao = 7
}
