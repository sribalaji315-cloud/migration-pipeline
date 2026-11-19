using Haworth.MdPipeline.Core.Rules;

namespace Haworth.MdPipeline.Pipeline.Options;

public sealed class PipelineOptions
{
    public const string Section = "Pipeline";

    public string DataDir { get; set; } = "data";
    public string FixturesDir { get; set; } = "../tests/fixtures";
    public string DuckDbMemoryLimit { get; set; } = "8GB";
    public int DuckDbThreads { get; set; } = 8;
    public int MappingBuckets { get; set; } = 64;
    public int MaxBomDepth { get; set; } = 100;
    public string? AnalysisCron { get; set; }

    /// <summary>List vs. free candidate: Either (default), ClassVariable or GlobalVariable decides (VARIABLEVALUES = VALUE_LIST).</summary>
    public string ListRule { get; set; } = "Either";

    /// <summary>R4: mappings without legacy_reachable (not sent by the live API) count as reachable.</summary>
    public bool LegacyReachableNullIncluded { get; set; } = true;

    /// <summary>REVIEW issues block wave export (§17, confirmed).</summary>
    public bool ReviewBlocksExport { get; set; } = true;
}

public sealed class MigrationToolOptions
{
    public const string Section = "MigrationTool";

    public string BaseUrl { get; set; } = "http://localhost:8000";
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>Static bearer token (e.g. copied from the Migration Tool). Used only when no User/Password is configured.</summary>
    public string BearerToken { get; set; } = string.Empty;

    /// <summary>Item id used by Test connection with a bearer token (GET /workspace-mappings/{id}); 200 or 404 both prove the token works.</summary>
    public string ProbeItemId { get; set; } = "__md_pipeline_probe__";

    /// <summary>User + password win over a static token: the login (POST /auth/login) renews itself, a pasted token expires.</summary>
    public bool HasLogin => !string.IsNullOrWhiteSpace(User) && !string.IsNullOrWhiteSpace(Password);

    public bool HasBearerToken => !HasLogin && !string.IsNullOrWhiteSpace(BearerToken);
    public int MaxConcurrency { get; set; } = 5;
    public bool UseBulkEndpoint { get; set; } = true;
    public bool UseFixtures { get; set; }
    public int BulkPageSize { get; set; } = 5000;
    public int ItemsBatchSize { get; set; } = 500;

    /// <summary>Page size for GET /bom/items (the Migration Tool item list; ~4 s per 2,000 items).</summary>
    public int ItemsPageSize { get; set; } = 2000;
}

public sealed class SwingOptions
{
    public const string Section = "Swing";

    public bool Enabled { get; set; } = true;
    public string OdbcConnectionString { get; set; } = string.Empty;
    public string ItemSql { get; set; } =
        "SELECT ARANR, ARMONR, ARABZ1, ARABZ2, ARPROB, ARPROG, ARARGR, ARMEIN, ARMART, ARVAKZ, ARBDID FROM {library}.GSAR00";
    public string BomSql { get; set; } =
        "SELECT STANR, STBKAR, STBKPO, STANRU, STMNRU, STMNG, STFORM, STMEIN, STAUSF, STAUSM, STAGPO, STFIST, STABJJ, STABMM, STABTT, STBIJJ, STBIMM, STBITT, STTXLN, STBDID FROM {library}.GSST00";
    public string Library { get; set; } = "CONFIGURE_LIBRARY";
    public List<string> AllowedLibraries { get; set; } = [];
    public int CommandTimeoutSeconds { get; set; } = 1800;
    public Dictionary<string, string> TypeCodes { get; set; } = new() { ["0"] = "FIXED", ["2"] = "CONFIGURABLE" };

    /// <summary>Explicit STFIST map. Unconfirmed encoding: unmapped values (including blank) raise PSEUDO_FLAG_UNKNOWN.</summary>
    public Dictionary<string, bool> PseudoFlags { get; set; } = new() { ["Y"] = true, ["N"] = false };

    /// <summary>Raw "year/month/day" component patterns confirmed to mean open-ended. Empty by default (unconfirmed).</summary>
    public List<string> OpenEndedDateIn { get; set; } = [];
    public List<string> OpenEndedDateOut { get; set; } = [];

    /// <summary>Two-digit years are unresolved unless a pivot is configured (yy &lt;= pivot → 20yy, else 19yy).</summary>
    public int? TwoDigitYearPivot { get; set; }

    /// <summary>Provisional rule: date_out &gt;= effective_date. Confirm before production.</summary>
    public bool DateOutInclusive { get; set; } = true;

    public SwingInterpretation ToInterpretation() =>
        SwingInterpretation.Create(TypeCodes, PseudoFlags, OpenEndedDateIn, OpenEndedDateOut, TwoDigitYearPivot, DateOutInclusive);
}
