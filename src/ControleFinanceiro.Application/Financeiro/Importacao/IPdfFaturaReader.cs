namespace ControleFinanceiro.Application.Financeiro.Importacao;

public interface IPdfFaturaReader
{
    Task<CsvFaturaParser.ParseResult> ParseAsync(Stream stream, CancellationToken cancellationToken);
}
