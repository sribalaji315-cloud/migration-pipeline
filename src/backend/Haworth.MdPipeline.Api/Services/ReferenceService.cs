using Haworth.MdPipeline.Api.Contracts;
using Haworth.MdPipeline.Core.Domain;
using Haworth.MdPipeline.Core.Domain.Metadata;
using Haworth.MdPipeline.Core.Interfaces;
using Haworth.MdPipeline.Data;
using Haworth.MdPipeline.Pipeline.Sources;
using Microsoft.EntityFrameworkCore;

namespace Haworth.MdPipeline.Api.Services;

public sealed class ReferenceService(
    MdPipelineDbContext db,
    AuditService audit,
    DataPaths paths,
    BluestarReferenceImporter importer,
    TimeProvider clock)
{
    public async Task<ReferenceVersionDto> UploadAsync(IFormFileCollection form, string actorId, CancellationToken ct)
    {
        var files = new Dictionary<BluestarFileKind, Stream>();
        try
        {
            foreach (var spec in BluestarContract.Files)
            {
                var file = form.FirstOrDefault(f => string.Equals(f.Name, spec.Kind.ToString(), StringComparison.OrdinalIgnoreCase))
                           ?? form.FirstOrDefault(f => string.Equals(f.FileName, spec.FileName, StringComparison.OrdinalIgnoreCase));
                if (file is not null)
                {
                    files[spec.Kind] = file.OpenReadStream();
                }
            }

            var version = (await db.ReferenceVersions.MaxAsync(v => (int?)v.Version, ct) ?? 0) + 1;
            var staging = paths.ReferenceStagingDir(version);
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }

            IReadOnlyList<ReferenceFileResult> results;
            try
            {
                results = await importer.ImportAsync(version, staging, files, ct);
                await DataPaths.PublishDirectoryAsync(staging, paths.ReferenceDir(version), ct);
            }
            catch
            {
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, recursive: true);
                }

                throw;
            }

            var entity = new ReferenceVersion
            {
                Version = version,
                Path = paths.ReferenceDir(version),
                UploadedAt = clock.GetUtcNow().UtcDateTime,
                UploadedByUserId = actorId,
                FilesJson = DtoMapping.ToFilesJson(results.Select(r => new ReferenceFileDto(r.Kind, r.FileName, r.BronzeTable, r.Rows, r.Sha256)))
            };
            db.ReferenceVersions.Add(entity);
            audit.Write(nameof(ReferenceVersion), version.ToString(), "Uploaded", actorId,
                $"Bluestar reference bs-{version}: " + string.Join(", ", results.Select(r => $"{r.BronzeTable}={r.Rows}")));
            await db.SaveChangesAsync(ct);
            return entity.ToDto();
        }
        finally
        {
            foreach (var stream in files.Values)
            {
                await stream.DisposeAsync();
            }
        }
    }

    public async Task<IReadOnlyList<ReferenceVersionDto>> VersionsAsync(CancellationToken ct) =>
        (await db.ReferenceVersions.OrderByDescending(v => v.Version).ToListAsync(ct)).Select(v => v.ToDto()).ToList();
}

public sealed class HealthService(IMigrationToolClient migrationTool, ISwingHealthProbe swing, DataPaths paths)
{
    public async Task<HealthDto> CheckAsync(CancellationToken ct)
    {
        var checks = new List<HealthCheckDto>();
        foreach (var check in await Task.WhenAll(migrationTool.CheckAsync(ct), swing.CheckAsync(ct)))
        {
            checks.Add(new HealthCheckDto(check.Name, check.Healthy, check.Detail));
        }

        checks.Add(CheckDataDir());
        return new HealthDto(checks.All(c => c.Healthy), checks);
    }

    private HealthCheckDto CheckDataDir()
    {
        try
        {
            Directory.CreateDirectory(paths.DataDir);
            var probe = Path.Combine(paths.DataDir, $".probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return new HealthCheckDto("DataDir", true, paths.DataDir);
        }
        catch (Exception ex)
        {
            return new HealthCheckDto("DataDir", false, ex.Message);
        }
    }
}
