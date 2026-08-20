using Microsoft.Extensions.Logging;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering
{
    internal sealed class DefaultIntakeBuffer : IIntakeBuffer
    {
        private readonly ILogger<DefaultIntakeBuffer> _logger;

        public DefaultIntakeBuffer(ILogger<DefaultIntakeBuffer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task EnqueueWorkAsync(WorkItem workItem, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning(
                "Runtime work item for artifact '{artifactId}' was ignored because no intake buffer is registered.",
                workItem?.ArtifactId);

            return Task.CompletedTask;
        }
    }
}
