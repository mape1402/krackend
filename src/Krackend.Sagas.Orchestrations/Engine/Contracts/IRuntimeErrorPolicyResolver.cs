using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Resolves the runtime action required by a task error policy.
/// </summary>
public interface IRuntimeErrorPolicyResolver
{
    /// <summary>
    /// Resolves an error policy into an executable runtime decision.
    /// </summary>
    /// <param name="policy">Task error policy.</param>
    /// <returns>Runtime decision.</returns>
    RuntimeErrorPolicyDecision Resolve(OnErrorPolicy policy);
}
