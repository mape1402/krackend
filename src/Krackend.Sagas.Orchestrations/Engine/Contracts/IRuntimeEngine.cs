namespace Krackend.Sagas.Orchestrations.Engine;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;

/// <summary>
/// Executes runtime ingress actions into orchestration state transitions.
/// </summary>
public interface IRuntimeEngine
{
    /// <summary>
    /// Processes a trigger intake item that is already protected by a durable ingress action.
    /// </summary>
    /// <param name="item">Trigger intake item to promote into a runtime instance.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Processing result for the provided intake item.</returns>
    Task<RuntimeEngineProcessResult> Process(TriggerIntakeBufferItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Continues a waiting orchestration instance from a correlated messaging response.
    /// </summary>
    /// <param name="command">Correlated messaging response command.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Processing result for the resumed instance.</returns>
    Task<RuntimeEngineProcessResult> ContinueFromResponse(RuntimeMessageResponseCommand command, CancellationToken cancellationToken = default);
}
