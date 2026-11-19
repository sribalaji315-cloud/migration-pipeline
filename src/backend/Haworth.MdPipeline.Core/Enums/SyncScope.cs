namespace Haworth.MdPipeline.Core.Enums;

/// <summary>Which sources a sync run extracts. Single-source scopes carry the other source's Bronze files forward from the previous snapshot.</summary>
public enum SyncScope
{
    All,
    SwingOnly,
    MigrationToolOnly
}
