using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.RetryStrategies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TimeoutBehaviorPolicies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Infrastructure;

/// <summary>
/// Builds typed default values for complex domain structures required by create and update commands.
/// </summary>
internal static class DefinitionDefaults
{
    /// <summary>
    /// Creates a default execution condition.
    /// </summary>
    /// <returns>Default execution condition.</returns>
    public static ExecutionCondition CreateExecutionCondition()
    {
        return new ExecutionCondition
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration
            {
                Expression = new Expression("true")
            }
        };
    }

    /// <summary>
    /// Creates a default retry policy.
    /// </summary>
    /// <returns>Default retry policy.</returns>
    public static RetryPolicy CreateRetryPolicy()
    {
        return new RetryPolicy
        {
            MaxRetries = 0,
            StrategyType = RetryStrategyType.Fixed,
            Strategy = new FixedRetryStrategy
            {
                Delay = Duration.FromSeconds(1)
            },
            StopOnNonRetryableError = false
        };
    }

    /// <summary>
    /// Creates a default timeout policy.
    /// </summary>
    /// <returns>Default timeout policy.</returns>
    public static TimeoutPolicy CreateTimeoutPolicy()
    {
        return new TimeoutPolicy
        {
            Timeout = Duration.FromSeconds(30),
            TimeoutBehavior = TimeoutBehavior.Fail,
            TimeoutBehaviorPolicy = new FailTimeoutBehaviorPolicy
            {
                ErrorCode = "TIMEOUT"
            }
        };
    }

    /// <summary>
    /// Creates a default transformation definition.
    /// </summary>
    /// <returns>Default transformation definition.</returns>
    public static TransformationDefinition CreateTransformationDefinition()
    {
        return new TransformationDefinition
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration()
        };
    }

    /// <summary>
    /// Creates a task configuration for the selected task kind.
    /// </summary>
    /// <param name="kind">Task kind.</param>
    /// <returns>Task configuration implementation.</returns>
    public static ITaskConfiguration CreateTaskConfiguration(TaskKind kind)
    {
        return kind switch
        {
            TaskKind.Http => new HttpTaskConfiguration
            {
                BaseUrlVariableRef = "vars.baseUrl",
                RelativePath = "/",
                Method = "GET",
                ExpectedStatusCodes = new List<int> { 200 }
            },
            TaskKind.Messaging => new MessagingTaskConfiguration
            {
                Topic = "orchestrator.topic",
                Version = new SemanticVersion(1, 0, 0)
            },
            TaskKind.Plugin => new PluginTaskConfiguration
            {
                PluginId = Id.New()
            },
            _ => new HumanApprovalTaskConfiguration()
        };
    }

    /// <summary>
    /// Creates a default compensation definition.
    /// </summary>
    /// <param name="kind">Compensation task kind.</param>
    /// <returns>Default compensation definition.</returns>
    public static CompensationDefinition CreateCompensationDefinition(TaskKind kind)
    {
        return new CompensationDefinition
        {
            CompensationTaskKind = kind,
            Transformation = CreateTransformationDefinition(),
            ExecutionCondition = CreateExecutionCondition(),
            Configuration = CreateTaskConfiguration(kind),
            RetryPolicy = CreateRetryPolicy(),
            TimeoutPolicy = CreateTimeoutPolicy(),
            DispatchType = TaskDispatchType.FireAndWait
        };
    }
}
