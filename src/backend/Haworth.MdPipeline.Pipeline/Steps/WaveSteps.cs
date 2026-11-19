using ClosedXML.Excel;
using Haworth.MdPipeline.Pipeline.Duck;
using Haworth.MdPipeline.Pipeline.Options;
using Microsoft.Extensions.Options;

namespace Haworth.MdPipeline.Pipeline.Steps;

/// <summary>What a wave export writes and where. Items = imported items of the union of the top BOM explosions.</summary>
public sealed class WaveExportTarget
{
    public required string WaveId { get; init; }
    public required IReadOnlyList<string> TopBoms { get; init; }
    public required IReadOnlyList<string> Items { get; init; }
    public required string GoldPath { get; init; }
    public required string ExportDir { get; init; }

    /// <summary>Rows per sheet / file written, filled by the steps for the manifest.</summary>
    public Dictionary<string, long> RowCounts { get; } = new(StringComparer.Ordinal);
    public List<string> Files { get; } = [];
    public List<string> ReusedGlobalVariables { get; } = [];
    public List<string> ReusedValueLists { get; } = [];
}

/// <summary>W1: Gold tables filtered to the wave, built in a separate DuckDB with the analysis Silver attached read-only.</summary>
public sealed class WaveGoldStep(IOptions<PipelineOptions> options) : IPipelineStep
{
    public string Name => nameof(WaveGoldStep);

    public IReadOnlyList<string> OutputTables => WaveExportStep.Sheets.Select(s => s.Table).ToList();

    public Task<StepResult> ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        var wave = ctx.Wave ?? throw new InvalidOperationException("WaveGoldStep requires a wave export target.");
        Directory.CreateDirectory(Path.GetDirectoryName(wave.GoldPath)!);
        if (File.Exists(wave.GoldPath))
        {
            File.Delete(wave.GoldPath);
        }

        using var duck = DuckDbExecutor.Open(wave.GoldPath, options.Value);
        duck.ExecuteScript("CREATE TABLE cfg_wave_top (top_item_id VARCHAR); CREATE TABLE cfg_wave_item (item_id VARCHAR);");
        duck.Append("cfg_wave_top", wave.TopBoms.Select(t => new object?[] { t }));
        duck.Append("cfg_wave_item", wave.Items.Select(i => new object?[] { i }));
        duck.ExecuteFile("w1_gold.sql", new Dictionary<string, string> { ["silver_path"] = SqlScriptLoader.SqlPath(ctx.SilverPath) });

        foreach (var sheet in WaveExportStep.Sheets)
        {
            wave.RowCounts[sheet.Name] = duck.CountRows(sheet.Table);
        }

        wave.ReusedGlobalVariables.AddRange(duck.Query("SELECT DISTINCT GlobalVariable FROM g02_class_attribute WHERE GlobalVariable IS NOT NULL ORDER BY 1")
            .Select(r => (string)r["GlobalVariable"]!));
        wave.ReusedValueLists.AddRange(duck.Query("SELECT DISTINCT ValueListId FROM g02_class_attribute WHERE ValueListId IS NOT NULL ORDER BY 1")
            .Select(r => (string)r["ValueListId"]!));
        return Task.FromResult(new StepResult(duck.CountRows("g03_engineering_object")));
    }
}

/// <summary>W2: one workbook per Gold sheet (§8.3) plus ISSUES_&lt;wave&gt;.xlsx (groups + one detail sheet per fix source).</summary>
public sealed class WaveExportStep(IOptions<PipelineOptions> options) : IPipelineStep
{
    public sealed record SheetSpec(string Name, string Table);

    public static readonly IReadOnlyList<SheetSpec> Sheets =
    [
        new("01_class", "g01_class"),
        new("02_class_attribute", "g02_class_attribute"),
        new("03_engineering_object", "g03_engineering_object"),
        new("04_engineering_object_attribute_value", "g04_engineering_object_attribute_value"),
        new("05_inheritance_link", "g05_inheritance_link"),
        new("06_eoav_condition", "g06_eoav_condition")
    ];

    public string Name => nameof(WaveExportStep);

    public IReadOnlyList<string> OutputTables => [.. Sheets.Select(s => s.Name + ".xlsx"), "ISSUES_<wave>.xlsx"];

    public static string IssuesFileName(string waveId) => $"ISSUES_{waveId}.xlsx";

    public Task<StepResult> ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        var wave = ctx.Wave ?? throw new InvalidOperationException("WaveExportStep requires a wave export target.");
        Directory.CreateDirectory(wave.ExportDir);
        using var duck = DuckDbExecutor.OpenReadOnly(wave.GoldPath, options.Value);
        long rows = 0;
        foreach (var sheet in Sheets)
        {
            ct.ThrowIfCancellationRequested();
            using var book = new XLWorkbook();
            rows += WriteSheet(book, sheet.Name, duck.Query($"SELECT * FROM {sheet.Table}"));
            Save(book, wave, sheet.Name + ".xlsx");
        }

        using (var issues = new XLWorkbook())
        {
            WriteSheet(issues, "Groups", duck.Query("SELECT * FROM g_issue_groups"));
            var detail = duck.Query("SELECT * FROM g_issues");
            foreach (var group in detail.GroupBy(r => (string)r["fix_source"]!).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                WriteSheet(issues, group.Key, group.ToList());
            }

            wave.RowCounts["ISSUES"] = detail.Count;
            Save(issues, wave, IssuesFileName(wave.WaveId));
        }

        return Task.FromResult(new StepResult(rows));
    }

    private static void Save(XLWorkbook book, WaveExportTarget wave, string fileName)
    {
        book.SaveAs(Path.Combine(wave.ExportDir, fileName));
        wave.Files.Add(fileName);
    }

    private static long WriteSheet(XLWorkbook book, string name, IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var sheet = book.AddWorksheet(name.Length > 31 ? name[..31] : name);
        if (rows.Count == 0)
        {
            return 0;
        }

        var columns = rows[0].Keys.ToList();
        for (var c = 0; c < columns.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = columns[c];
        }

        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = ToCell(rows[r][columns[c]]);
            }
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        return rows.Count;
    }

    // Codes stay text: Excel must not turn "0010" or "1E5" into numbers.
    private static XLCellValue ToCell(object? value) => value switch
    {
        null => Blank.Value,
        bool b => b,
        DateOnly d => d.ToString("yyyy-MM-dd"),
        DateTime dt => dt.ToString("yyyy-MM-dd"),
        int or long or short => Convert.ToInt64(value),
        double or decimal or float => Convert.ToDouble(value),
        _ => value.ToString()
    };
}
