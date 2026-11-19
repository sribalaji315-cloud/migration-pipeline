using System.Threading.Channels;

namespace Haworth.MdPipeline.Api.Jobs;

/// <summary>In-process run queue. Analysis runs are processed one at a time by <see cref="PipelineJobHost"/>.</summary>
public sealed class RunQueue
{
    private readonly Channel<string> channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(string runId, CancellationToken ct = default) => channel.Writer.WriteAsync(runId, ct);

    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken ct) => channel.Reader.ReadAllAsync(ct);
}

public sealed class PipelineJobHost(RunQueue queue, IServiceScopeFactory scopes, ILogger<PipelineJobHost> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var runId in queue.ReadAllAsync(stoppingToken))
        {
            using var scope = scopes.CreateScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<PipelineRunner>().ExecuteAsync(runId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Run {RunId} failed.", runId);
            }
        }
    }
}
