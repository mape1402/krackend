using Krackend.Sagas.Orchestrations.Runtime.Distribution;

namespace Krackend.Sagas.Orchestrations.Runtime.Api;

/// <summary>
/// Represents a runtime design node status update request.
/// </summary>
public sealed record RuntimeDesignNodeStatusRequest(RuntimeDesignNodeStatus Status);
