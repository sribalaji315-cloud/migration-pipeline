using Haworth.MdPipeline.Core.Domain.Metadata;
using Haworth.MdPipeline.Data.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Haworth.MdPipeline.Data;

/// <summary>
/// Metadata / control plane only. Mapping-scale data lives in DuckDB + Parquet, linked by keys and paths.
/// User ids are stored without FKs so scheduled runs can use the "system" actor.
/// </summary>
public class MdPipelineDbContext(DbContextOptions<MdPipelineDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<BronzeVersion> BronzeVersions => Set<BronzeVersion>();
    public DbSet<ReferenceVersion> ReferenceVersions => Set<ReferenceVersion>();
    public DbSet<PipelineRun> PipelineRuns => Set<PipelineRun>();
    public DbSet<PipelineRunStep> PipelineRunSteps => Set<PipelineRunStep>();
    public DbSet<IssueGroup> IssueGroups => Set<IssueGroup>();
    public DbSet<TypeDecision> TypeDecisions => Set<TypeDecision>();
    public DbSet<Wave> Waves => Set<Wave>();
    public DbSet<ExportedItem> ExportedItems => Set<ExportedItem>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<BronzeVersion>(e =>
        {
            e.HasKey(x => x.Version);
            e.Property(x => x.Version).ValueGeneratedNever();
            e.Property(x => x.Path).IsRequired();
        });

        builder.Entity<ReferenceVersion>(e =>
        {
            e.HasKey(x => x.Version);
            e.Property(x => x.Version).ValueGeneratedNever();
            e.Property(x => x.Path).IsRequired();
        });

        builder.Entity<PipelineRun>(e =>
        {
            e.HasKey(x => x.RunId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.QueuedAt);
            e.HasOne<BronzeVersion>().WithMany().HasForeignKey(x => x.BronzeVersion).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ReferenceVersion>().WithMany().HasForeignKey(x => x.ReferenceVersion).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.SourceAnalysisRunId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Wave>().WithMany().HasForeignKey(x => x.WaveId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Steps).WithOne().HasForeignKey(s => s.RunId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PipelineRunStep>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RunId, x.Sequence }).IsUnique();
        });

        builder.Entity<IssueGroup>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.GroupKey, x.IssueType }).IsUnique();
            e.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.FirstSeenRunId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.LastSeenRunId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.ResolvedInRunId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TypeDecision>(e =>
        {
            e.HasKey(x => x.ItemId);
            e.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.LastSeenRunId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Wave>(e =>
        {
            e.HasKey(x => x.WaveId);
            e.HasOne<BronzeVersion>().WithMany().HasForeignKey(x => x.BronzeVersion).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ReferenceVersion>().WithMany().HasForeignKey(x => x.ReferenceVersion).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.LastExportRunId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ExportedItem>(e =>
        {
            e.HasKey(x => new { x.ItemId, x.WaveId });
            e.HasOne<Wave>().WithMany().HasForeignKey(x => x.WaveId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditLogEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
        });
    }
}
