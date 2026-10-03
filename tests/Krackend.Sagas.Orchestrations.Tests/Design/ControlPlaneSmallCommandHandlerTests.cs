using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Design;

public sealed class ControlPlaneSmallCommandHandlerTests
{
    [Fact]
    public async Task StageEnableDisableHandlersDelegateToRepository()
    {
        var repository = Substitute.For<IStageRepository>();
        var stageId = Id.New();

        var enabled = await new EnableStageDefinitionCommandHandler(repository)
            .Handle(new EnableStageDefinitionCommand(stageId.ToString()), CancellationToken.None);
        var disabled = await new DisableStageDefinitionCommandHandler(repository)
            .Handle(new DisableStageDefinitionCommand(stageId.ToString()), CancellationToken.None);

        Assert.True(enabled);
        Assert.True(disabled);
        await repository.Received(1).SetIsEnabled(stageId, true, Arg.Any<CancellationToken>());
        await repository.Received(1).SetIsEnabled(stageId, false, Arg.Any<CancellationToken>());
        Assert.Throws<ArgumentNullException>(() => new EnableStageDefinitionCommandHandler(null!));
        Assert.Throws<ArgumentNullException>(() => new DisableStageDefinitionCommandHandler(null!));
    }

    [Fact]
    public async Task TaskCompensationHandlersDelegateConditionAndTransformation()
    {
        var repository = Substitute.For<ITaskRepository>();
        var taskId = Id.New();
        var condition = DslCondition("$failed");
        var transformation = DslTransformation("map compensation");

        var conditionResult = await new SetTaskCompensationExecutionConditionCommandHandler(repository)
            .Handle(new SetTaskCompensationExecutionConditionCommand(taskId.ToString(), condition), CancellationToken.None);
        var transformationResult = await new SetTaskCompensationTransformationCommandHandler(repository)
            .Handle(new SetTaskCompensationTransformationCommand(taskId.ToString(), transformation), CancellationToken.None);

        Assert.True(conditionResult);
        Assert.True(transformationResult);
        await repository.Received(1).SetCompensationExecutionCondition(taskId, condition, Arg.Any<CancellationToken>());
        await repository.Received(1).SetCompensationTransformation(taskId, transformation, Arg.Any<CancellationToken>());
        Assert.Throws<ArgumentNullException>(() => new SetTaskCompensationExecutionConditionCommandHandler(null!));
        Assert.Throws<ArgumentNullException>(() => new SetTaskCompensationTransformationCommandHandler(null!));
    }

    [Fact]
    public async Task TriggerCompensationHandlersDelegateConditionAndTransformation()
    {
        var repository = Substitute.For<ITriggerBindingRepository>();
        var triggerId = Id.New();
        var condition = DslCondition("$trigger.Valid");
        var transformation = DslTransformation("map trigger compensation");

        var conditionResult = await new SetTriggerCompensationExecutionConditionCommandHandler(repository)
            .Handle(new SetTriggerCompensationExecutionConditionCommand(triggerId.ToString(), condition), CancellationToken.None);
        var transformationResult = await new SetTriggerCompensationTransformationCommandHandler(repository)
            .Handle(new SetTriggerCompensationTransformationCommand(triggerId.ToString(), transformation), CancellationToken.None);

        Assert.True(conditionResult);
        Assert.True(transformationResult);
        await repository.Received(1).SetCompensationExecutionCondition(triggerId, condition, Arg.Any<CancellationToken>());
        await repository.Received(1).SetCompensationTransformation(triggerId, transformation, Arg.Any<CancellationToken>());
        Assert.Throws<ArgumentNullException>(() => new SetTriggerCompensationExecutionConditionCommandHandler(null!));
        Assert.Throws<ArgumentNullException>(() => new SetTriggerCompensationTransformationCommandHandler(null!));
    }

    private static ExecutionCondition DslCondition(string expression)
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration
            {
                Expression = new Expression(expression)
            }
        };

    private static TransformationDefinition DslTransformation(string dsl)
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration
            {
                Dsl = dsl
            }
        };
}
