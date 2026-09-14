namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control
{
    /// <summary>
    /// Evaluates orchestration state and returns the next runtime decisions.
    /// </summary>
    public interface IDecisionControl
    {
        /// <summary>
        /// Decides the next actions for the supplied orchestration state.
        /// </summary>
        /// <param name="request">Decision request containing the current orchestration context.</param>
        /// <param name="cancellationToken">Token used to cancel the decision evaluation.</param>
        /// <returns>The decisions that should be executed by the runtime.</returns>
        Task<IReadOnlyCollection<IDecision>> DecideAsync(DecisionRequest request, CancellationToken cancellationToken);
    }
}
