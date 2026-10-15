namespace ControleFinanceiro.Application.Financeiro.Importacao;

public static class PdfFaturaUpload
{
    public const long MaxBytes = 128L * 1024 * 1024;
    public const long MaxRequestBytes = MaxBytes + 1024 * 1024;
}
