using System.Threading.Channels;
using Nextflow.Domain.Interfaces.Application.Services;

namespace Nextflow.Infrastructure.Services.Forecast;

/// <summary>Fila em memória (singleton) das análises; um único leitor, o <see cref="ForecastRunWorker"/>.</summary>
public sealed class ForecastQueue : IForecastQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(Guid runId, CancellationToken ct = default) => _channel.Writer.WriteAsync(runId, ct);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
