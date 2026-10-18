namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Reflection;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

public sealed class RuntimeDefensiveBranchTests
{
    [Fact]
    public void CompleteStageHandlerPreservesNullBranchDiagnostics()
    {
        var metadata = new Dictionary<string, JsonNode>();

        InvokeCopyDiagnostics("CompleteStageDecisionHandler", metadata, "Branch");

        Assert.True(metadata.ContainsKey("Branch.reason"));
        Assert.Null(metadata["Branch.reason"]);
    }

    [Fact]
    public void DispatchTaskHandlerPreservesNullConditionDiagnostics()
    {
        var metadata = new Dictionary<string, JsonNode>();

        InvokeCopyDiagnostics("DispatchTaskDecisionHandler", metadata, "Condition");

        Assert.True(metadata.ContainsKey("Condition.reason"));
        Assert.Null(metadata["Condition.reason"]);
    }

    [Fact]
    public void StartStageHandlerPreservesNullConditionDiagnostics()
    {
        var metadata = new Dictionary<string, JsonNode>();

        InvokeCopyDiagnostics("StartStageDecisionHandler", metadata, "Condition");

        Assert.True(metadata.ContainsKey("Condition.reason"));
        Assert.Null(metadata["Condition.reason"]);
    }

    [Fact]
    public void DecisionControlBuildsAFallbackDeadLetterReasonWhenNoErrorCodeExists()
    {
        var assembly = typeof(RuntimeArtifactDeploymentService).Assembly;
        var type = assembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Engine.Control.DecisionControl",
            throwOnError: true)!;
        var method = type.GetMethod("ResolveDeadLetterReason", BindingFlags.NonPublic | BindingFlags.Static)!;

        var reason = (string)method.Invoke(null,
        [
            new OrchestrationInstance
            {
                OrchestrationDefinitionKey = "orders_fulfillment",
                CorrelationId = "corr-1",
                SagaId = "saga-1",
                ExecutionKey = "orders_fulfillment:corr-1",
                ErrorSummary = string.Empty
            },
            new TaskExecution { TaskKey = "reserve_inventory" }
        ])!;

        Assert.Contains("no retry", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DecisionControlIncludesTheMappedErrorCodeInTheDeadLetterReason()
    {
        var assembly = typeof(RuntimeArtifactDeploymentService).Assembly;
        var type = assembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Engine.Control.DecisionControl",
            throwOnError: true)!;
        var method = type.GetMethod("ResolveDeadLetterReason", BindingFlags.NonPublic | BindingFlags.Static)!;

        var reason = (string)method.Invoke(null,
        [
            new OrchestrationInstance
            {
                OrchestrationDefinitionKey = "orders_fulfillment",
                CorrelationId = "corr-1",
                SagaId = "saga-1",
                ExecutionKey = "orders_fulfillment:corr-1",
                ErrorSummary = string.Empty
            },
            new TaskExecution
            {
                TaskKey = "reserve_inventory",
                Metadata = new Dictionary<string, JsonNode>
                {
                    ["ExecutionErrorCode"] = JsonValue.Create("InventoryUnavailable")
                }
            }
        ])!;

        Assert.Contains("InventoryUnavailable", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TimeoutProcessorUsesTheInstanceIdentifierWhenSagaIdIsBlank()
    {
        var type = typeof(RuntimeArtifactDeploymentService).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts.DefaultOrchestrationTimeoutProcessor",
            throwOnError: true)!;
        var method = type.GetMethod("GetSagaId", BindingFlags.NonPublic | BindingFlags.Static)!;
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "orders_fulfillment",
            CorrelationId = "corr-1",
            SagaId = " ",
            ExecutionKey = "orders_fulfillment:corr-1"
        };

        var sagaId = (string)method.Invoke(null, [instance])!;

        Assert.Equal(instance.Id.ToString(), sagaId);
    }

    [Fact]
    public void RuntimeArtifactDeploymentRejectsRolloutForLifecycleArtifacts()
    {
        var method = typeof(RuntimeArtifactDeploymentService).GetMethod(
            "EnsureDirectLifecycleStrategy",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, ["Rollout"]));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public void TriggerClaimRepositoryNormalizesNullAndDuplicateIdentities()
    {
        var type = typeof(RuntimeDbContext).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories.RuntimeTriggerClaimRepository",
            throwOnError: true)!;
        var method = type.GetMethod("Normalize", BindingFlags.NonPublic | BindingFlags.Static)!;

        var empty = (string[])method.Invoke(null, [null!])!;
        var normalized = (string[])method.Invoke(null, [new[] { " messaging|orders.created|1.0.0 ", "MESSAGING|ORDERS.CREATED|1.0.0" }])!;

        Assert.Empty(empty);
        Assert.Equal(["MESSAGING|ORDERS.CREATED|1.0.0"], normalized);
    }

    private static void InvokeCopyDiagnostics(
        string handlerName,
        IDictionary<string, JsonNode> metadata,
        string prefix)
    {
        var type = typeof(RuntimeArtifactDeploymentService).Assembly.GetType(
            $"Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers.{handlerName}",
            throwOnError: true)!;
        var method = type.GetMethod("CopyDiagnostics", BindingFlags.NonPublic | BindingFlags.Static)!;
        var diagnostics = new Dictionary<string, JsonNode> { ["reason"] = null! };
        var arguments = handlerName == "CompleteStageDecisionHandler"
            ? new object?[] { metadata, diagnostics }
            : [metadata, diagnostics, prefix];
        method.Invoke(null, arguments);
    }
}
