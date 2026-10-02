namespace Krackend.Sagas.Orchestrations.Runtime.Execution;

using Krackend.Sagas.Orchestrations.Abstractions.Execution;

internal sealed class ExecutionPolicyResolutionResult
{
    public bool Succeeded { get; init; }

    public string ErrorCode { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public ResolvedExecutionPolicyArtifact ResolvedPolicy { get; init; }

    public IExecutionSandboxProvider Provider { get; init; }

    public static ExecutionPolicyResolutionResult Success(
        ResolvedExecutionPolicyArtifact policy,
        IExecutionSandboxProvider provider)
        => new()
        {
            Succeeded = true,
            ResolvedPolicy = policy,
            Provider = provider
        };

    public static ExecutionPolicyResolutionResult Failure(string code, string message)
        => new()
        {
            Succeeded = false,
            ErrorCode = code,
            ErrorMessage = message
        };
}

