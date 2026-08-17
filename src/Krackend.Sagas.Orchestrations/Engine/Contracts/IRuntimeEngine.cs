namespace Krackend.Sagas.Orchestrations.Engine;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;

/// <summary>
/// Processes pending runtime trigger intake items into orchestration executions.
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
    /// Processes the next pending intake item when one is available.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Processing result for the next intake item.</returns>
    Task<RuntimeEngineProcessResult> ProcessNext(CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes pending intake items up to the provided limit.
    /// </summary>
    /// <param name="maxItems">Maximum number of intake items to process.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Processing results for the handled intake items.</returns>
    Task<IReadOnlyCollection<RuntimeEngineProcessResult>> ProcessAll(int maxItems = 25, CancellationToken cancellationToken = default);

    /// <summary>
    /// Continues a waiting orchestration instance from a correlated messaging response.
    /// </summary>
    /// <param name="command">Correlated messaging response command.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Processing result for the resumed instance.</returns>
    Task<RuntimeEngineProcessResult> ContinueFromResponse(RuntimeMessageResponseCommand command, CancellationToken cancellationToken = default);
}
