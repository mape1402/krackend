using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Runtime decision produced from a configured task error policy.
/// </summary>
/// <param name="Policy">Configured error policy.</param>
/// <param name="Action">Runtime action to apply.</param>
public sealed record RuntimeErrorPolicyDecision(OnErrorPolicy Policy, RuntimeErrorPolicyAction Action);
