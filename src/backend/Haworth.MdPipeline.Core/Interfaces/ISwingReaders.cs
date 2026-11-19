namespace Haworth.MdPipeline.Core.Interfaces;

/// <summary>
/// Raw tabular source extract. Values are preserved as source text; normalization happens in Silver SQL.
/// </summary>
public sealed record SourceExtract(string SourceTable, IReadOnlyList<string> Columns, IAsyncEnumerable<string?[]> Rows);

public static class SwingContract
{
    public const string BomTable = "GSST00";
    public const string ItemTable = "GSAR00";

    public static readonly IReadOnlyList<string> Gsst00Columns =
    [
        "STANR", "STBKAR", "STBKPO", "STANRU", "STMNRU",
        "STMNG", "STFORM", "STMEIN", "STAUSF", "STAUSM", "STAGPO",
        "STFIST", "STABJJ", "STABMM", "STABTT", "STBIJJ", "STBIMM", "STBITT",
        "STTXLN", "STBDID"
    ];

    public static readonly IReadOnlyList<string> Gsar00Columns =
    [
        "ARANR", "ARMONR", "ARABZ1", "ARABZ2", "ARPROB", "ARPROG",
        "ARARGR", "ARMEIN", "ARMART", "ARVAKZ", "ARBDID"
    ];
}

public interface ISwingBomReader
{
    SourceExtract ReadBom(CancellationToken ct);
}

public interface ISwingItemReader
{
    SourceExtract ReadItems(CancellationToken ct);
}

public interface ISwingHealthProbe
{
    Task<SourceHealth> CheckAsync(CancellationToken ct);
}

public sealed record SourceHealth(string Name, bool Healthy, string Detail);
