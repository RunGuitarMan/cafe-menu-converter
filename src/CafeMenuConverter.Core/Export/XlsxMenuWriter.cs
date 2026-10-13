using System.Globalization;
using CafeMenuConverter.Core.Model;
using CafeMenuConverter.Core.Parsing;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CafeMenuConverter.Core.Export;

public static class XlsxMenuWriter
{
    public static IReadOnlyList<string> Headers { get; } = Array.AsReadOnly(new[]
    {
        "office", "date", "type", "category", "name", "product_composition", "weight", "unit", "quantity", "price",
        "calories", "protein", "fats", "carbohydrates"
    });

    /// <summary>Build next to the destination, then replace it atomically. Parsing errors never create a workbook.</summary>
    public static void Write(string path, ConversionResult result, Office office, bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        if (!result.Success) throw new InvalidOperationException("Сохранение невозможно: в меню есть ошибки или нет блюд.");
        if (!Office.All.Any(o => o.Id == office.Id)) throw new ArgumentException("Выберите один из трёх офисов.", nameof(office));
        path = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Файл результата должен иметь расширение .xlsx.", nameof(path));
        if (!overwrite && File.Exists(path)) throw new IOException("Файл уже существует. Подтвердите перезапись или выберите другое имя.");
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
                    Build(document, result.Items, office, cancellationToken);
                stream.Flush(true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static string[] Values(MenuItem item, Office office) =>
    [
        office.Id, item.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), item.MealType, item.Category,
        item.Name, JsonList.Serialize(item.Composition), JsonList.Serialize(item.Weights), item.Unit, item.Quantity,
        item.Price, item.Calories, item.Protein, item.Fats, item.Carbohydrates
    ];

    private static void Build(SpreadsheetDocument document, IReadOnlyList<MenuItem> items, Office office, CancellationToken token)
    {
        var workbook = document.AddWorkbookPart();
        workbook.Workbook = new Workbook();
        var styles = workbook.AddNewPart<WorkbookStylesPart>();
        styles.Stylesheet = new Stylesheet(
            new Fonts(new Font(new FontSize { Val = 11 }, new FontName { Val = "Calibri" }),
                new Font(new Bold(), new FontSize { Val = 11 }, new FontName { Val = "Calibri" })) { Count = 2 },
            new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
                new Fill(new PatternFill(new ForegroundColor { Rgb = "FFD3D3D3" }, new BackgroundColor { Indexed = 64 })
                    { PatternType = PatternValues.Solid })) { Count = 3 },
            new Borders(new Border()) { Count = 1 },
            new CellStyleFormats(new CellFormat()) { Count = 1 },
            new CellFormats(new CellFormat { NumberFormatId = 49, ApplyNumberFormat = true },
                new CellFormat(new Alignment { Horizontal = HorizontalAlignmentValues.Center })
                { FontId = 1, FillId = 2, ApplyFont = true, ApplyFill = true, ApplyAlignment = true, NumberFormatId = 49, ApplyNumberFormat = true }) { Count = 2 });
        styles.Stylesheet.Save();
        var strings = workbook.AddNewPart<SharedStringTablePart>();
        strings.SharedStringTable = new SharedStringTable();
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        uint count = 0;
        Cell Cell(string value, int column, uint row)
        {
            if (value.Length > 32767) throw new InvalidOperationException($"Строка {row}: текст превышает ограничение Excel (32767 символов).");
            if (!indices.TryGetValue(value, out var index))
            {
                index = indices.Count;
                indices.Add(value, index);
                strings.SharedStringTable.Append(new SharedStringItem(new Text(value) { Space = SpaceProcessingModeValues.Preserve }));
            }
            count++;
            return new Cell { CellReference = $"{(char)('A' + column)}{row}", DataType = CellValues.SharedString,
                CellValue = new CellValue(index.ToString(CultureInfo.InvariantCulture)), StyleIndex = row == 1 ? 1U : 0U };
        }
        var sheet = workbook.AddNewPart<WorksheetPart>();
        var data = new SheetData();
        var header = new Row { RowIndex = 1 };
        header.Append(Headers.Select((value, i) => Cell(value, i, 1)));
        data.Append(header);
        uint rowIndex = 2;
        foreach (var item in items)
        {
            token.ThrowIfCancellationRequested();
            var row = new Row { RowIndex = rowIndex };
            row.Append(Values(item, office).Select((value, i) => Cell(value, i, rowIndex)));
            data.Append(row);
            rowIndex++;
        }
        var widths = new double[] { 38, 13, 12, 36, 60, 80, 20, 8, 10, 10, 10, 10, 10, 16 };
        sheet.Worksheet = new Worksheet(new SheetViews(new SheetView(new Pane
        { VerticalSplit = 1, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen }) { WorkbookViewId = 0 }),
            new Columns(widths.Select((width, i) => new Column { Min = (uint)i + 1, Max = (uint)i + 1, Width = width, CustomWidth = true })),
            data, new AutoFilter { Reference = $"A1:N{rowIndex - 1}" });
        workbook.Workbook.Append(new Sheets(new Sheet { Id = workbook.GetIdOfPart(sheet), SheetId = 1, Name = "Data" }));
        strings.SharedStringTable.Count = count;
        strings.SharedStringTable.UniqueCount = (uint)indices.Count;
        strings.SharedStringTable.Save();
        sheet.Worksheet.Save();
        workbook.Workbook.Save();
    }
}
