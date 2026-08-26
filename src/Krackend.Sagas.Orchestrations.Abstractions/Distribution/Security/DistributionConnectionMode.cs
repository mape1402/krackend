namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Represents how a Design node and Runtime node exchange artifacts.
/// </summary>
public enum DistributionConnectionMode
{
    /// <summary>
    /// Design initiates calls and publishes artifacts into Runtime.
    /// </summary>
    DesignPublishesToRuntime = 1,

    /// <summary>
    /// Runtime initiates calls and fetches releases from Design.
    /// </summary>
    RuntimeFetchesFromDesign = 2,

    /// <summary>
    /// Both Design and Runtime can initiate artifact distribution calls.
    /// </summary>
    HybridSync = 3
}
