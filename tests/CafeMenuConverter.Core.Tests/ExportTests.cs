using CafeMenuConverter.Core.Export;
using CafeMenuConverter.Core.Model;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;

namespace CafeMenuConverter.Core.Tests;

public class ExportTests
{
    [Fact]
    public void Xlsx_round_trip_matches_every_value_and_reference_header()
    {
        var result = ConversionTests.Read("breakfast.pdf");
        var path = Path.Combine(Path.GetTempPath(), $"menu-{Guid.NewGuid():N}.xlsx");
        try
        {
            XlsxMenuWriter.Write(path, result, Office.Vodny);
            using var document = SpreadsheetDocument.Open(path, false);
            Assert.Empty(new OpenXmlValidator().Validate(document));
            var workbook = document.WorkbookPart!;
            Assert.Equal("Data", Assert.Single(workbook.Workbook!.Sheets!.Elements<Sheet>()).Name!.Value);
            var strings = workbook.SharedStringTablePart!.SharedStringTable!.Elements<SharedStringItem>().Select(i => i.InnerText).ToArray();
            var rows = workbook.WorksheetParts.Single().Worksheet!.Descendants<Row>().ToArray();
            Assert.Equal(146, rows.Length);
            var expected = new[] { XlsxMenuWriter.Headers.ToArray() }.Concat(result.Items.Select(i => XlsxMenuWriter.Values(i, Office.Vodny))).ToArray();
            for (var r = 0; r < rows.Length; r++)
            {
                var cells = rows[r].Elements<Cell>().ToArray();
                Assert.Equal(14, cells.Length);
                Assert.All(cells, c => Assert.Equal(CellValues.SharedString, c.DataType!.Value));
                Assert.Equal(expected[r], cells.Select(c => strings[int.Parse(c.CellValue!.Text)]));
            }
            var style = workbook.WorkbookStylesPart!.Stylesheet!;
            Assert.NotNull(style.Fonts!.Elements<Font>().ElementAt(1).Bold);
            Assert.Equal("FFD3D3D3", style.Fills!.Elements<Fill>().ElementAt(2).PatternFill!.ForegroundColor!.Rgb!.Value);
            using var reference = SpreadsheetDocument.Open(ConversionTests.Fixture("reference.xlsx"), false);
            var referenceStrings = reference.WorkbookPart!.SharedStringTablePart!.SharedStringTable!.Elements<SharedStringItem>().Select(i => i.InnerText).ToArray();
            Assert.Equal(XlsxMenuWriter.Headers, reference.WorkbookPart.WorksheetParts.Single().Worksheet!.Descendants<Row>().First().Elements<Cell>().Select(c => referenceStrings[int.Parse(c.CellValue!.Text)]));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Errors_cancellation_and_unconfirmed_overwrite_preserve_destination()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "existing.xlsx");
        File.WriteAllText(path, "original");
        try
        {
            var result = ConversionTests.Read("breakfast.pdf");
            Assert.Throws<IOException>(() => XlsxMenuWriter.Write(path, result, Office.Space));
            Assert.Throws<InvalidOperationException>(() => XlsxMenuWriter.Write(path, new ConversionResult([], []), Office.Space, true));
            Assert.Throws<OperationCanceledException>(() => XlsxMenuWriter.Write(path, result, Office.Space, true, new CancellationToken(true)));
            Assert.Equal("original", File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(directory));
            Assert.Throws<ArgumentException>(() => XlsxMenuWriter.Write(Path.ChangeExtension(path, ".pdf"), result, Office.Space));
            XlsxMenuWriter.Write(path, result, Office.Space, true);
            using var document = SpreadsheetDocument.Open(path, false);
            Assert.NotNull(document.WorkbookPart);
        }
        finally { Directory.Delete(directory, true); }
    }
}
