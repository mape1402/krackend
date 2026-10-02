namespace Krackend.Sagas.Orchestrations.Runtime.Execution;

internal interface IExecutionPolicyResolver
{
    ExecutionPolicyResolutionResult Resolve(ExecutionPolicyResolutionRequest request);
}

