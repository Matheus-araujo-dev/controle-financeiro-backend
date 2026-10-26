namespace ControleFinanceiro.Application.FinanceAI;

public interface IContentGenerator
{
    Task<byte[]> GerarGraficoAsync(ChartRequest request, CancellationToken ct = default);
    Task<byte[]> GerarExcelAsync(ExcelRequest request, CancellationToken ct = default);
    Task<byte[]> GerarPdfAsync(PdfRequest request, CancellationToken ct = default);
    Task<byte[]> GerarApresentacaoAsync(ApresentacaoRequest request, CancellationToken ct = default);
}

public sealed record ChartRequest(
    string Tipo,
    string Titulo,
    IReadOnlyList<ChartDataPoint> Dados,
    int Largura = 800,
    int Altura = 600);

public sealed record ChartDataPoint(string Label, double Valor, string? Cor = null);

public sealed record ExcelRequest(
    string Titulo,
    IReadOnlyList<ExcelSheet> Planilhas);

public sealed record ExcelSheet(
    string Nome,
    IReadOnlyList<string> Colunas,
    IReadOnlyList<IReadOnlyList<object?>> Linhas);

public sealed record PdfRequest(
    string Titulo,
    string Conteudo,
    string? Subtitulo = null);

public sealed record ApresentacaoRequest(
    string Titulo,
    IReadOnlyList<SlideData> Slides);

public sealed record SlideData(
    string Titulo,
    string Conteudo,
    string? Notas = null);
