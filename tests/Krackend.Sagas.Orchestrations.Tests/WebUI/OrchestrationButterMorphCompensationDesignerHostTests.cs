namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

using global::ButterMorph.Web.Razor;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;
using NSubstitute;

public sealed class OrchestrationButterMorphCompensationDesignerHostTests
{
    [Fact]
    public void ParserFormatsAndParsesCompensationContexts()
    {
        var parser = new OrchestrationButterMorphDesignerContextParser();

        var taskTransformationKey = parser.FormatTaskCompensationTransformation(" version-1 ", " task-1 ");
        var triggerTransformationKey = parser.FormatTriggerCompensationTransformation("version-2", "trigger-1");
        var taskConditionKey = parser.FormatTaskCompensationExecutionCondition("version-3", "task-2");
        var triggerConditionKey = parser.FormatTriggerCompensationExecutionCondition("version-4", "trigger-2");

        Assert.Equal("orchestration-task-compensation-transform:version-1:task-1", taskTransformationKey);
        Assert.True(parser.TryParseTaskCompensationTransformation(taskTransformationKey, out var taskTransformation));
        Assert.Equal("version-1", taskTransformation.OrchestrationVersionId);
        Assert.Equal("task-1", taskTransformation.TaskDefinitionId);

        Assert.True(parser.TryParseTriggerCompensationTransformation(triggerTransformationKey, out var triggerTransformation));
        Assert.Equal("version-2", triggerTransformation.OrchestrationVersionId);
        Assert.Equal("trigger-1", triggerTransformation.TriggerBindingId);

        Assert.True(parser.TryParseTaskCompensationExecutionCondition(taskConditionKey, out var taskCondition));
        Assert.Equal("version-3", taskCondition.OrchestrationVersionId);
        Assert.Equal("task-2", taskCondition.TaskDefinitionId);

        Assert.True(parser.TryParseTriggerCompensationExecutionCondition(triggerConditionKey, out var triggerCondition));
        Assert.Equal("version-4", triggerCondition.OrchestrationVersionId);
        Assert.Equal("trigger-2", triggerCondition.TriggerBindingId);

        Assert.False(parser.TryParseTriggerCompensationTransformation(taskTransformationKey, out _));
        Assert.False(parser.TryParseTaskCompensationExecutionCondition("orchestration-task-compensation-execution-condition:missing", out _));
        Assert.Throws<ArgumentException>(() => parser.FormatTaskCompensationTransformation(string.Empty, "task-1"));
        Assert.Throws<ArgumentException>(() => parser.FormatTriggerCompensationExecutionCondition("version-1", " "));
    }

    [Fact]
    public async Task TransformationHostSavesTaskAndTriggerCompensationTransformations()
    {
        var parser = new OrchestrationButterMorphDesignerContextParser();
        var schemaContextService = Substitute.For<IOrchestrationSchemaContextApplicationService>();
        var taskApplicationService = Substitute.For<ITaskApplicationService>();
        var triggerBindingApplicationService = Substitute.For<ITriggerBindingApplicationService>();
        var host = new OrchestrationButterMorphDesignerHost(
            schemaContextService,
            taskApplicationService,
            triggerBindingApplicationService,
            parser,
            Substitute.For<IOrchestrationButterMorphSchemaImporter>(),
            Substitute.For<IOrchestrationButterMorphSourceMetadataFactory>());

        schemaContextService.GetForTaskCompensation(
                Arg.Is<GetTaskCompensationSchemaContextQuery>(x =>
                    x.OrchestrationVersionId == "version-1" &&
                    x.TaskDefinitionId == "task-1"))
            .Returns(CreateSchemaContext());
        schemaContextService.GetForTriggerCompensation(
                Arg.Is<GetTriggerCompensationSchemaContextQuery>(x =>
                    x.OrchestrationVersionId == "version-1" &&
                    x.TriggerBindingId == "trigger-1"))
            .Returns(CreateSchemaContext());
        taskApplicationService.SetCompensationTransformation(Arg.Any<SetTaskCompensationTransformationCommand>())
            .Returns(true);
        triggerBindingApplicationService.SetCompensationTransformation(Arg.Any<SetTriggerCompensationTransformationCommand>())
            .Returns(true);

        var taskResult = await host.Save(new ButterMorphDesignerSaveRequest
        {
            ContextKey = parser.FormatTaskCompensationTransformation("version-1", "task-1"),
            DslContent = "map task compensation"
        });
        var triggerResult = await host.Save(new ButterMorphDesignerSaveRequest
        {
            ContextKey = parser.FormatTriggerCompensationTransformation("version-1", "trigger-1"),
            DslContent = "map trigger compensation"
        });

        Assert.True(taskResult.Succeeded);
        Assert.True(triggerResult.Succeeded);
        await taskApplicationService.Received(1).SetCompensationTransformation(
            Arg.Is<SetTaskCompensationTransformationCommand>(x =>
                MatchesTransformation(x, "task-1", "map task compensation")));
        await triggerBindingApplicationService.Received(1).SetCompensationTransformation(
            Arg.Is<SetTriggerCompensationTransformationCommand>(x =>
                MatchesTransformation(x, "trigger-1", "map trigger compensation")));
    }

    [Fact]
    public async Task ValidationHostSavesTaskAndTriggerCompensationConditions()
    {
        var parser = new OrchestrationButterMorphDesignerContextParser();
        var taskApplicationService = Substitute.For<ITaskApplicationService>();
        var triggerBindingApplicationService = Substitute.For<ITriggerBindingApplicationService>();
        var host = new OrchestrationButterMorphValidationDesignerHost(
            Substitute.For<IOrchestrationSchemaContextApplicationService>(),
            Substitute.For<IStageApplicationService>(),
            taskApplicationService,
            triggerBindingApplicationService,
            parser,
            Substitute.For<IOrchestrationButterMorphSchemaImporter>(),
            Substitute.For<IOrchestrationButterMorphSourceMetadataFactory>());

        taskApplicationService.SetCompensationExecutionCondition(Arg.Any<SetTaskCompensationExecutionConditionCommand>())
            .Returns(true);
        triggerBindingApplicationService.SetCompensationExecutionCondition(Arg.Any<SetTriggerCompensationExecutionConditionCommand>())
            .Returns(true);

        var taskResult = await host.Save(new ButterMorphValidationDesignerSaveRequest
        {
            ContextKey = parser.FormatTaskCompensationExecutionCondition("version-1", "task-1"),
            DslContent = "task.reply.ok == true"
        });
        var triggerResult = await host.Save(new ButterMorphValidationDesignerSaveRequest
        {
            ContextKey = parser.FormatTriggerCompensationExecutionCondition("version-1", "trigger-1"),
            DslContent = "trigger.metadata.retryable == true"
        });

        Assert.True(taskResult.Succeeded);
        Assert.True(triggerResult.Succeeded);
        await taskApplicationService.Received(1).SetCompensationExecutionCondition(
            Arg.Is<SetTaskCompensationExecutionConditionCommand>(x =>
                MatchesCondition(x, "task-1", "task.reply.ok == true")));
        await triggerBindingApplicationService.Received(1).SetCompensationExecutionCondition(
            Arg.Is<SetTriggerCompensationExecutionConditionCommand>(x =>
                MatchesCondition(x, "trigger-1", "trigger.metadata.retryable == true")));
    }

    private static OrchestrationSchemaContext CreateSchemaContext()
        => new()
        {
            OrchestrationVersionId = "version-1",
            OrchestrationVersion = "1.0.0",
            StageKey = "stage-1",
            TaskKey = "task-1",
            Signature = "schema-signature"
        };

    private static bool MatchesTransformation(
        SetTaskCompensationTransformationCommand command,
        string id,
        string dsl)
        => command.Id == id &&
            command.Transformation.Configuration is DslTransformationConfiguration configuration &&
            configuration.Dsl == dsl &&
            configuration.SourceContextHash == "schema-signature";

    private static bool MatchesTransformation(
        SetTriggerCompensationTransformationCommand command,
        string id,
        string dsl)
        => command.Id == id &&
            command.Transformation.Configuration is DslTransformationConfiguration configuration &&
            configuration.Dsl == dsl &&
            configuration.SourceContextHash == "schema-signature";

    private static bool MatchesCondition(
        SetTaskCompensationExecutionConditionCommand command,
        string id,
        string dsl)
        => command.Id == id &&
            command.ExecutionCondition.Configuration is DslConditionConfiguration configuration &&
            configuration.Expression.ToString() == dsl;

    private static bool MatchesCondition(
        SetTriggerCompensationExecutionConditionCommand command,
        string id,
        string dsl)
        => command.Id == id &&
            command.ExecutionCondition.Configuration is DslConditionConfiguration configuration &&
            configuration.Expression.ToString() == dsl;
}
