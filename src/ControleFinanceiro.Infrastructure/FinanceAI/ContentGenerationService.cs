using ClosedXML.Excel;
using ControleFinanceiro.Application.FinanceAI;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ScottPlot;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace ControleFinanceiro.Infrastructure.FinanceAI;

public sealed class ContentGenerationService : IContentGenerator
{
    static ContentGenerationService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> GerarGraficoAsync(ChartRequest request, CancellationToken ct)
    {
        var plot = new Plot();
        plot.Title(request.Titulo);

        var labels = request.Dados.Select(d => d.Label).ToArray();
        var values = request.Dados.Select(d => d.Valor).ToArray();

        switch (request.Tipo.ToLowerInvariant())
        {
            case "pizza":
            {
                var slices = request.Dados.Select(d =>
                {
                    var slice = new PieSlice { Value = d.Valor, Label = d.Label };
                    if (d.Cor is not null && d.Cor.StartsWith('#'))
                    {
                        var c = System.Drawing.ColorTranslator.FromHtml(d.Cor);
                        slice.FillColor = new ScottPlot.Color(c.R, c.G, c.B);
                    }
                    return slice;
                }).ToList();

                var pie = plot.Add.Pie(slices);
                pie.ShowSliceLabels = true;
                break;
            }
            case "barra":
            {
                var positions = Enumerable.Range(0, values.Length).Select(i => (double)i).ToArray();
                var bars = plot.Add.Bars(positions, values);
                plot.Axes.Bottom.SetTicks(positions, labels);
                plot.Axes.Bottom.TickLabelStyle.Rotation = labels.Any(l => l.Length > 10) ? -45 : 0;

                if (request.Dados.Any(d => d.Cor is not null))
                {
                    for (var i = 0; i < bars.Bars.Count && i < request.Dados.Count; i++)
                    {
                        if (request.Dados[i].Cor is not null && request.Dados[i].Cor!.StartsWith('#'))
                        {
                            var c = System.Drawing.ColorTranslator.FromHtml(request.Dados[i].Cor!);
                            bars.Bars[i].FillColor = new ScottPlot.Color(c.R, c.G, c.B);
                        }
                    }
                }
                break;
            }
            case "linha":
            {
                var xs = Enumerable.Range(0, values.Length).Select(i => (double)i).ToArray();
                plot.Add.Scatter(xs, values);
                plot.Axes.Bottom.SetTicks(xs, labels);
                break;
            }
            case "area":
            {
                var xs = Enumerable.Range(0, values.Length).Select(i => (double)i).ToArray();
                var scatter = plot.Add.Scatter(xs, values);
                scatter.FillY = true;
                scatter.FillYValue = 0;
                plot.Axes.Bottom.SetTicks(xs, labels);
                break;
            }
            default:
            {
                var positions2 = Enumerable.Range(0, values.Length).Select(i => (double)i).ToArray();
                plot.Add.Bars(positions2, values);
                plot.Axes.Bottom.SetTicks(positions2, labels);
                break;
            }
        }

        var bytes = plot.GetImageBytes(request.Largura, request.Altura, ImageFormat.Png);
        return Task.FromResult(bytes);
    }

    public Task<byte[]> GerarExcelAsync(ExcelRequest request, CancellationToken ct)
    {
        using var workbook = new XLWorkbook();

        foreach (var sheet in request.Planilhas)
        {
            var ws = workbook.Worksheets.Add(sheet.Nome.Length > 31
                ? sheet.Nome[..31]
                : sheet.Nome);

            for (var col = 0; col < sheet.Colunas.Count; col++)
            {
                var cell = ws.Cell(1, col + 1);
                cell.Value = sheet.Colunas[col];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2B5797");
                cell.Style.Font.FontColor = XLColor.White;
            }

            for (var row = 0; row < sheet.Linhas.Count; row++)
            {
                var linha = sheet.Linhas[row];
                for (var col = 0; col < linha.Count; col++)
                {
                    var cell = ws.Cell(row + 2, col + 1);
                    var valor = linha[col];
                    if (valor is null)
                        continue;

                    var str = valor.ToString() ?? "";
                    if (double.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num))
                        cell.Value = num;
                    else if (DateTime.TryParse(str, out var dt))
                        cell.Value = dt;
                    else
                        cell.Value = str;
                }
            }

            ws.Columns().AdjustToContents();
            if (sheet.Colunas.Count > 0 && sheet.Linhas.Count > 0)
                ws.Range(1, 1, 1, sheet.Colunas.Count).SetAutoFilter();
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    public Task<byte[]> GerarPdfAsync(PdfRequest request, CancellationToken ct)
    {
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text(request.Titulo).Bold().FontSize(20).FontColor(Colors.Blue.Darken3);
                    if (!string.IsNullOrEmpty(request.Subtitulo))
                        col.Item().Text(request.Subtitulo).FontSize(14).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingBottom(5).LineHorizontal(1).LineColor(Colors.Blue.Darken3);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    var lines = request.Conteudo.Split('\n');
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed))
                        {
                            col.Item().PaddingVertical(4);
                            continue;
                        }

                        if (trimmed.StartsWith("## "))
                        {
                            col.Item().PaddingTop(10).Text(trimmed[3..]).Bold().FontSize(16).FontColor(Colors.Blue.Darken2);
                        }
                        else if (trimmed.StartsWith("# "))
                        {
                            col.Item().PaddingTop(12).Text(trimmed[2..]).Bold().FontSize(18).FontColor(Colors.Blue.Darken3);
                        }
                        else if (trimmed.StartsWith("- ") || trimmed.StartsWith("• "))
                        {
                            col.Item().PaddingLeft(15).Row(row =>
                            {
                                row.AutoItem().Text("•  ");
                                row.RelativeItem().Text(trimmed[2..]);
                            });
                        }
                        else if (trimmed.StartsWith("**") && trimmed.EndsWith("**"))
                        {
                            col.Item().Text(trimmed.Trim('*')).Bold();
                        }
                        else
                        {
                            col.Item().Text(trimmed);
                        }
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("ControleFinanceiro — Gerado em ");
                    text.Span(DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm"));
                    text.Span(" | Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    public Task<byte[]> GerarApresentacaoAsync(ApresentacaoRequest request, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        using (var pres = PresentationDocument.Create(ms, PresentationDocumentType.Presentation, true))
        {
            var presentationPart = pres.AddPresentationPart();
            presentationPart.Presentation = new Presentation();
            var slideIdList = new SlideIdList();
            presentationPart.Presentation.SlideIdList = slideIdList;

            var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>();
            var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>();

            slideLayoutPart.SlideLayout = new SlideLayout(new P.CommonSlideData(new P.ShapeTree()));
            slideMasterPart.SlideMaster = new SlideMaster(new P.CommonSlideData(new P.ShapeTree()));
            slideMasterPart.SlideMaster.Append(new SlideLayoutIdList(new SlideLayoutId { RelationshipId = slideMasterPart.GetIdOfPart(slideLayoutPart) }));

            presentationPart.Presentation.SlideMasterIdList = new SlideMasterIdList(
                new SlideMasterId { RelationshipId = presentationPart.GetIdOfPart(slideMasterPart) });

            uint slideId = 256;
            foreach (var slideData in request.Slides)
            {
                var slidePart = presentationPart.AddNewPart<SlidePart>();
                var slide = new Slide(new P.CommonSlideData(new P.ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new ApplicationNonVisualDrawingProperties()),
                    new P.GroupShapeProperties(new A.TransformGroup()),
                    CreateTitleShape(slideData.Titulo),
                    CreateContentShape(slideData.Conteudo)
                )));

                slidePart.Slide = slide;

                if (!string.IsNullOrEmpty(slideData.Notas))
                {
                    var notesPart = slidePart.AddNewPart<NotesSlidePart>();
                    notesPart.NotesSlide = new NotesSlide(new P.CommonSlideData(new P.ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(),
                        CreateNotesShape(slideData.Notas)
                    )));
                }

                slideIdList.Append(new SlideId
                {
                    Id = slideId++,
                    RelationshipId = presentationPart.GetIdOfPart(slidePart)
                });
            }

            presentationPart.Presentation.Save();
        }

        return Task.FromResult(ms.ToArray());
    }

    private static P.Shape CreateTitleShape(string title)
    {
        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = 2, Name = "Title" },
                new P.NonVisualShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(new A.Transform2D(
                new A.Offset { X = 457200, Y = 274638 },
                new A.Extents { Cx = 8229600, Cy = 1143000 })),
            new P.TextBody(
                new A.BodyProperties(),
                new A.Paragraph(
                    new A.Run(
                        new A.RunProperties { Language = "pt-BR", FontSize = 2800, Bold = true },
                        new A.Text { Text = title }))));
    }

    private static P.Shape CreateContentShape(string content)
    {
        var paragraphs = content.Split('\n')
            .Select(line =>
            {
                var trimmed = line.Trim();
                var isBullet = trimmed.StartsWith("- ") || trimmed.StartsWith("• ");
                var text = isBullet ? trimmed[2..] : trimmed;
                var para = new A.Paragraph(
                    new A.Run(
                        new A.RunProperties { Language = "pt-BR", FontSize = 1800 },
                        new A.Text { Text = text }));
                if (isBullet)
                    para.PrependChild(new A.ParagraphProperties(new A.BulletFont { Typeface = "Arial" }, new A.CharacterBullet { Char = "•" }));
                return para;
            })
            .ToArray();

        var body = new P.TextBody(new A.BodyProperties());
        foreach (var p in paragraphs)
            body.Append(p);

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = 3, Name = "Content" },
                new P.NonVisualShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(new A.Transform2D(
                new A.Offset { X = 457200, Y = 1600200 },
                new A.Extents { Cx = 8229600, Cy = 4525963 })),
            body);
    }

    private static P.Shape CreateNotesShape(string notes)
    {
        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = 2, Name = "Notes" },
                new P.NonVisualShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(),
            new P.TextBody(
                new A.BodyProperties(),
                new A.Paragraph(
                    new A.Run(
                        new A.RunProperties { Language = "pt-BR" },
                        new A.Text { Text = notes }))));
    }
}
