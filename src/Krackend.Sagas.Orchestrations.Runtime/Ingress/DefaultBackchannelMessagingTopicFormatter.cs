using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    internal sealed class DefaultBackchannelMessagingTopicFormatter : IBackchannelMessagingTopicFormatter
    {
        private readonly RuntimeIngressBackchannelOptions _options;

        public DefaultBackchannelMessagingTopicFormatter(RuntimeIngressBackchannelOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public string Format(OrchestrationArtifact artifact)
        {
            if (artifact is null)
            {
                throw new ArgumentNullException(nameof(artifact));
            }

            return $"{_options.TopicPrefix}.{artifact.Key}";
        }
    }
}
