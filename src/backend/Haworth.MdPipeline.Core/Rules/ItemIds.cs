namespace Haworth.MdPipeline.Core.Rules;

public static class ItemIds
{
    /// <summary>Trim only: leading zeros and case are preserved (GSST00/GSAR00 contract, section 18.5).</summary>
    public static string? Normalize(string? raw)
    {
        var trimmed = raw?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>Stable FNV-1a bucket used to partition mapping Parquet files.</summary>
    public static string Bucket(string itemId, int buckets = 64)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var ch in itemId)
        {
            hash ^= ch;
            hash *= prime;
        }

        return (hash % (uint)buckets).ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
    }
}
