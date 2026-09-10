namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    /// <summary>
    /// Represents a page of ingress configurations read for runtime standup.
    /// </summary>
    public class IngressConfigurationReadingResult
    {
        /// <summary>
        /// Gets a value indicating whether additional configuration pages are available.
        /// </summary>
        public bool HasMoreItems { get; init; }

        /// <summary>
        /// Gets the ingress configurations in the current page.
        /// </summary>
        public IReadOnlyCollection<IngressConfiguration> Configurations { get; init; }
    }
}
