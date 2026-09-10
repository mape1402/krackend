namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    /// <summary>
    /// Identifies how an ingress endpoint participates in orchestration flow.
    /// </summary>
    public enum IngressKind
    {
        /// <summary>
        /// The ingress starts a new orchestration instance.
        /// </summary>
        Trigger,

        /// <summary>
        /// The ingress receives task execution callbacks for existing orchestration instances.
        /// </summary>
        Backchannel
    }
}
