namespace Haworth.MdPipeline.Core.Domain.Metadata;

public class ReferenceVersion
{
    public int Version { get; set; }
    public string Path { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string UploadedByUserId { get; set; } = string.Empty;
    public string FilesJson { get; set; } = "[]";
}
