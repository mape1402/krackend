using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Default runtime error policy resolver.
/// </summary>
public sealed class RuntimeErrorPolicyResolver : IRuntimeErrorPolicyResolver
{
    /// <inheritdoc/>
    public RuntimeErrorPolicyDecision Resolve(OnErrorPolicy policy)
    {
        var action = policy switch
        {
            OnErrorPolicy.Continue => RuntimeErrorPolicyAction.Continue,
            OnErrorPolicy.StopAndCompensate => RuntimeErrorPolicyAction.StartCompensation,
            _ => RuntimeErrorPolicyAction.Stop
        };

        return new RuntimeErrorPolicyDecision(policy, action);
    }
}
