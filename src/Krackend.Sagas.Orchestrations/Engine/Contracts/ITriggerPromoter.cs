using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Promotes a trigger intake item into persisted runtime execution state.
/// </summary>
public interface ITriggerPromoter
{
    /// <summary>
    /// Promotes a trigger intake item into an orchestration instance.
    /// </summary>
    /// <param name="item">Buffered trigger intake item.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Promotion result with intake, instance and artifact information.</returns>
    Task<TriggerPromotionResult> Promote(TriggerIntakeBufferItem item, CancellationToken cancellationToken = default);
}
