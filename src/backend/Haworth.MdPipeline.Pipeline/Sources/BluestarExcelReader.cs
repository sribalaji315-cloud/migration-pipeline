using System.Globalization;
using ClosedXML.Excel;
using Haworth.MdPipeline.Core.Domain;
using Haworth.MdPipeline.Core.Interfaces;

namespace Haworth.MdPipeline.Pipeline.Sources;

public sealed class BluestarExcelReader : IBluestarReferenceReader
{
    public SourceExtract Read(BluestarFileSpec spec, Stream workbook)
    {
        using var book = new XLWorkbook(workbook);
        var sheet = book.Worksheets.First();
        var headerRow = sheet.FirstRowUsed()
            ?? throw new DomainValidationException($"'{spec.FileName}' has no header row.");

        var headers = headerRow.CellsUsed()
            .GroupBy(c => c.GetString().Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First().Address.ColumnNumber);
        var missing = spec.RequiredColumns.Where(c => !headers.ContainsKey(c)).ToList();
        if (missing.Count > 0)
        {
            throw new DomainValidationException(
                $"'{spec.FileName}' is missing required columns: {string.Join(", ", missing)}. Found: {string.Join(", ", headers.Keys)}.");
        }

        var columnNumbers = spec.Columns.Select(c => headers.TryGetValue(c, out var n) ? n : (int?)null).ToArray();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        var rows = new List<string?[]>();
        for (var r = headerRow.RowNumber() + 1; r <= lastRow; r++)
        {
            var values = columnNumbers.Select(c => c is { } col ? Raw(sheet.Cell(r, col)) : null).ToArray();
            if (values.Any(v => v is not null))
            {
                rows.Add(values);
            }
        }

        return new SourceExtract(spec.BronzeTable, spec.Columns, ToAsync(rows));
    }

    private static string? Raw(IXLCell cell) =>
        cell.Value.IsBlank ? null : cell.Value.ToString(CultureInfo.InvariantCulture);

#pragma warning disable CS1998
    private static async IAsyncEnumerable<string?[]> ToAsync(IEnumerable<string?[]> rows)
    {
        foreach (var row in rows)
        {
            yield return row;
        }
    }
#pragma warning restore CS1998
}
