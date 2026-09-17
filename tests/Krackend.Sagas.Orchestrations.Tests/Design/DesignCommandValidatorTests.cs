using FluentValidation;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.RetryStrategies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TimeoutBehaviorPolicies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;

namespace Krackend.Sagas.Orchestrations.Tests.Design;

public sealed class DesignCommandValidatorTests
{
    private const string ValidId = "01JMJGBJ0R7WFN9QBG3CCBEVM1";
    private const string ValidStageId = "01JMJGBJ0R7WFN9QBG3CCBEVM2";

    [Fact]
    public void TaskValidatorsAcceptFullyConfiguredMessagingTasks()
    {
        var create = new CreateTaskDefinitionCommandValidator();
        var update = new UpdateTaskDefinitionCommandValidator();

        Assert.True(create.Validate(CreateTask()).IsValid);
        Assert.True(update.Validate(UpdateTask()).IsValid);
        Assert.True(create.Validate(CreateTask() with { TimeoutPolicy = null! }).IsValid);
        Assert.True(update.Validate(UpdateTask() with { TimeoutPolicy = null! }).IsValid);
    }

    [Theory]
    [MemberData(nameof(InvalidTaskCreateCommands))]
    public void CreateTaskValidatorRejectsUnsupportedOrIncompleteMessagingConfiguration(CreateTaskDefinitionCommand command)
        => Assert.False(new CreateTaskDefinitionCommandValidator().Validate(command).IsValid);

    [Theory]
    [MemberData(nameof(InvalidTaskUpdateCommands))]
    public void UpdateTaskValidatorRejectsUnsupportedOrIncompleteMessagingConfiguration(UpdateTaskDefinitionCommand command)
        => Assert.False(new UpdateTaskDefinitionCommandValidator().Validate(command).IsValid);

    [Fact]
    public void StageValidatorsAcceptDslConditionsAndRejectUnsupportedInputs()
    {
        var validCondition = DslCondition("$trigger.Valid");
        var invalidCondition = new ExecutionCondition
        {
            Engine = EngineType.Plugin,
            Configuration = new DslConditionConfiguration { Expression = new Expression("true") }
        };

        Assert.True(new CreateStageDefinitionCommandValidator()
            .Validate(new CreateStageDefinitionCommand(ValidId, "fulfillment", "Fulfillment", "", 0, validCondition))
            .IsValid);
        Assert.True(new UpdateStageDefinitionCommandValidator()
            .Validate(new UpdateStageDefinitionCommand(ValidId, "fulfillment", "Fulfillment", "", 0, validCondition))
            .IsValid);
        Assert.True(new SetStageExecutionConditionCommandValidator()
            .Validate(new SetStageExecutionConditionCommand(ValidId, null!))
            .IsValid);

        Assert.False(new CreateStageDefinitionCommandValidator()
            .Validate(new CreateStageDefinitionCommand("bad", "", "", "", -1, invalidCondition))
            .IsValid);
        Assert.False(new UpdateStageDefinitionCommandValidator()
            .Validate(new UpdateStageDefinitionCommand("bad", "", "", "", -1, invalidCondition))
            .IsValid);
        Assert.False(new SetStageExecutionConditionCommandValidator()
            .Validate(new SetStageExecutionConditionCommand("bad", invalidCondition))
            .IsValid);
    }

    [Fact]
    public void TriggerValidatorsAcceptEventChannelsAndRejectInvalidChannels()
    {
        var create = new CreateTriggerBindingCommandValidator();
        var update = new UpdateTriggerBindingCommandValidator();
        var channel = new EventTriggerChannel
        {
            Topic = "events.sales.sale.created",
            Version = new SemanticVersion(1, 0, 0),
            HasValidation = true,
            Validation = DslValidation("$payload.saleId != null")
        };

        Assert.True(create.Validate(new CreateTriggerBindingCommand(ValidId, "sales.sale.created", TriggerType.Event, channel, true, "")).IsValid);
        Assert.True(update.Validate(new UpdateTriggerBindingCommand(ValidId, "sales.sale.created", TriggerType.Event, channel, true, "")).IsValid);
        Assert.False(create.Validate(new CreateTriggerBindingCommand("bad", "", (TriggerType)999, null!, true, "")).IsValid);
        Assert.False(update.Validate(new UpdateTriggerBindingCommand("bad", "", (TriggerType)999, new EventTriggerChannel { Topic = "", HasValidation = true }, true, "")).IsValid);
        Assert.False(create.Validate(new CreateTriggerBindingCommand(ValidId, "sales.sale.created", TriggerType.Event, new EventTriggerChannel
        {
            Topic = "events.sales.sale.created",
            HasValidation = true,
            Validation = DslValidation("")
        }, true, "")).IsValid);
        Assert.True(create.Validate(new CreateTriggerBindingCommand(ValidId, "sales.sale.created", TriggerType.Event, new EventTriggerChannel
        {
            Topic = "events.sales.sale.created"
        }, true, "")).IsValid);
        Assert.False(update.Validate(new UpdateTriggerBindingCommand(ValidId, "sales.sale.created", TriggerType.Event, new EventTriggerChannel
        {
            Topic = "events.sales.sale.created",
            HasValidation = true,
            Validation = new ValidationDefinition
            {
                Engine = EngineType.DSL,
                Configuration = new UnsupportedValidationConfiguration()
            }
        }, true, "")).IsValid);
    }

    public static IEnumerable<object[]> InvalidTaskCreateCommands()
    {
        yield return [CreateTask(stageId: "bad")];
        yield return [CreateTask(key: "", name: "", order: -1)];
        yield return [CreateTask(kind: TaskKind.Http)];
        yield return [CreateTask(dispatchType: TaskDispatchType.FireAndWait)];
        yield return [CreateTask() with { Configuration = null! }];
        yield return [CreateTask(configuration: new MessagingTaskConfiguration { Topic = "" })];
        yield return [CreateTask(configuration: new MessagingTaskConfiguration { Topic = "inventories.reserve", HasRequestValidation = true })];
        yield return [CreateTask(configuration: new MessagingTaskConfiguration
        {
            Topic = "inventories.reserve",
            HasRequestValidation = true,
            RequestValidation = new ValidationDefinition
            {
                Engine = EngineType.DSL,
                Configuration = new UnsupportedValidationConfiguration()
            }
        })];
        yield return [CreateTask(configuration: new MessagingTaskConfiguration
        {
            Topic = "inventories.reserve",
            HasResponseValidation = true,
            ResponseValidation = DslValidation("")
        })];
        yield return [CreateTask(condition: new ExecutionCondition { Engine = EngineType.Custom, Configuration = new DslConditionConfiguration { Expression = new Expression("true") } })];
        yield return [CreateTask(transformation: new TransformationDefinition { Engine = EngineType.Custom, Configuration = new DslTransformationConfiguration { Dsl = "{}" } })];
        yield return [CreateTask(retryPolicy: Retry(maxRetries: -1))];
        yield return [CreateTask(retryPolicy: new RetryPolicy { MaxRetries = 1, StrategyType = RetryStrategyType.Exponential, Strategy = FixedDelay(1) })];
        yield return [CreateTask(timeoutPolicy: new TimeoutPolicy { Timeout = Duration.FromSeconds(0), TimeoutBehavior = TimeoutBehavior.Fail, TimeoutBehaviorPolicy = new FailTimeoutBehaviorPolicy() })];
        yield return [CreateTask(timeoutPolicy: new TimeoutPolicy { Timeout = Duration.FromSeconds(1), TimeoutBehavior = TimeoutBehavior.Wait, TimeoutBehaviorPolicy = new WaitTimeoutBehaviorPolicy { WaitingTime = Duration.FromSeconds(0) } })];
        yield return [CreateTask(timeoutPolicy: new TimeoutPolicy { Timeout = Duration.FromSeconds(1), TimeoutBehavior = TimeoutBehavior.Reconcile, TimeoutBehaviorPolicy = new ReconcileTimeoutBehaviorPolicy { RetryPolicy = Retry(maxRetries: -1) } })];
        yield return [CreateTask(timeoutPolicy: new TimeoutPolicy { Timeout = Duration.FromSeconds(1), TimeoutBehavior = TimeoutBehavior.Fail, TimeoutBehaviorPolicy = new UnsupportedTimeoutBehaviorPolicy() })];
        yield return [CreateTask(compensation: new CompensationDefinition { CompensationTaskKind = TaskKind.Http, DispatchType = TaskDispatchType.FireAndForget, Configuration = Messaging("release") })];
        yield return [CreateTask(compensation: new CompensationDefinition { CompensationTaskKind = TaskKind.Messaging, DispatchType = TaskDispatchType.FireAndWaitCallback, Configuration = Messaging("release") })];
    }

    public static IEnumerable<object[]> InvalidTaskUpdateCommands()
    {
        foreach (var data in InvalidTaskCreateCommands().Skip(1))
        {
            var command = (CreateTaskDefinitionCommand)data[0];
            yield return [UpdateFromCreate(command)];
        }

        yield return [UpdateTask(id: "bad")];
    }

    private static CreateTaskDefinitionCommand CreateTask(
        string stageId = ValidStageId,
        string key = "inventories.reserve",
        string name = "Reserve inventory",
        int order = 0,
        TaskKind kind = TaskKind.Messaging,
        TaskDispatchType dispatchType = TaskDispatchType.FireAndWaitCallback,
        ITaskConfiguration? configuration = null,
        ExecutionCondition? condition = null,
        TransformationDefinition? transformation = null,
        RetryPolicy? retryPolicy = null,
        TimeoutPolicy? timeoutPolicy = null,
        CompensationDefinition? compensation = null)
        => new(
            stageId,
            key,
            name,
            order,
            "",
            kind,
            TaskExecutionMode.Sequential,
            ValidId,
            condition ?? DslCondition("$trigger.Valid"),
            transformation ?? DslTransformation("{}"),
            configuration ?? Messaging("inventories.reserve", withValidations: true),
            retryPolicy ?? Retry(),
            timeoutPolicy ?? Timeout(TimeoutBehavior.Fail),
            OnErrorPolicy.Stop,
            compensation ?? Compensation(),
            dispatchType,
            true);

    private static UpdateTaskDefinitionCommand UpdateTask(
        string id = ValidId,
        string key = "inventories.reserve",
        string name = "Reserve inventory",
        int order = 0,
        TaskKind kind = TaskKind.Messaging,
        TaskDispatchType dispatchType = TaskDispatchType.FireAndWaitCallback,
        ITaskConfiguration? configuration = null,
        ExecutionCondition? condition = null,
        TransformationDefinition? transformation = null,
        RetryPolicy? retryPolicy = null,
        TimeoutPolicy? timeoutPolicy = null,
        CompensationDefinition? compensation = null)
        => new(
            id,
            key,
            name,
            order,
            "",
            kind,
            TaskExecutionMode.Sequential,
            ValidId,
            condition ?? DslCondition("$trigger.Valid"),
            transformation ?? DslTransformation("{}"),
            configuration ?? Messaging("inventories.reserve", withValidations: true),
            retryPolicy ?? Retry(),
            timeoutPolicy ?? Timeout(TimeoutBehavior.Wait),
            OnErrorPolicy.Stop,
            compensation ?? Compensation(),
            dispatchType,
            true);

    private static UpdateTaskDefinitionCommand UpdateFromCreate(CreateTaskDefinitionCommand command)
        => new(
            ValidId,
            command.Key,
            command.Name,
            command.Order,
            command.Notes,
            command.Kind,
            command.ExecutionMode,
            command.ParallelGroupId,
            command.ExecutionCondition,
            command.Transformation,
            command.Configuration,
            command.RetryPolicy,
            command.TimeoutPolicy,
            command.OnErrorPolicy,
            command.CompensationDefinition,
            command.DispatchType,
            command.IsEnabled);

    private static MessagingTaskConfiguration Messaging(string topic, bool withValidations = false)
        => new()
        {
            Topic = topic,
            Version = new SemanticVersion(1, 0, 0),
            HasRequestValidation = withValidations,
            RequestValidation = withValidations ? DslValidation("$payload.sku != null") : null,
            HasResponseValidation = withValidations,
            ResponseValidation = withValidations ? DslValidation("$payload.reserved == true") : null
        };

    private static ExecutionCondition DslCondition(string expression)
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration { Expression = new Expression(expression) }
        };

    private static TransformationDefinition DslTransformation(string dsl)
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration { Dsl = dsl }
        };

    private static ValidationDefinition DslValidation(string dsl)
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslValidationConfiguration { Dsl = dsl }
        };

    private static RetryPolicy Retry(int maxRetries = 3)
        => new()
        {
            MaxRetries = maxRetries,
            StrategyType = RetryStrategyType.Fixed,
            Strategy = FixedDelay(1)
        };

    private static FixedRetryStrategy FixedDelay(double seconds)
        => new() { Delay = Duration.FromSeconds(seconds) };

    private static TimeoutPolicy Timeout(TimeoutBehavior behavior)
        => behavior switch
        {
            TimeoutBehavior.Wait => new TimeoutPolicy
            {
                Timeout = Duration.FromSeconds(30),
                TimeoutBehavior = TimeoutBehavior.Wait,
                TimeoutBehaviorPolicy = new WaitTimeoutBehaviorPolicy { WaitingTime = Duration.FromSeconds(5) }
            },
            TimeoutBehavior.Reconcile => new TimeoutPolicy
            {
                Timeout = Duration.FromSeconds(30),
                TimeoutBehavior = TimeoutBehavior.Reconcile,
                TimeoutBehaviorPolicy = new ReconcileTimeoutBehaviorPolicy { RetryPolicy = Retry(1) }
            },
            _ => new TimeoutPolicy
            {
                Timeout = Duration.FromSeconds(30),
                TimeoutBehavior = TimeoutBehavior.Fail,
                TimeoutBehaviorPolicy = new FailTimeoutBehaviorPolicy()
            }
        };

    private static CompensationDefinition Compensation()
        => new()
        {
            CompensationTaskKind = TaskKind.Messaging,
            DispatchType = TaskDispatchType.FireAndForget,
            Configuration = Messaging("inventories.release"),
            ExecutionCondition = DslCondition("$context.ShouldCompensate"),
            Transformation = DslTransformation("{}"),
            RetryPolicy = Retry(1),
            TimeoutPolicy = Timeout(TimeoutBehavior.Reconcile)
        };

    private sealed class UnsupportedValidationConfiguration : IValidationConfiguration
    {
        public EngineType Engine => EngineType.DSL;
    }

    private sealed class UnsupportedTimeoutBehaviorPolicy : ITimeoutBehaviorPolicy
    {
        public TimeoutBehavior Behavior => TimeoutBehavior.Fail;
    }
}
