namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

using global::ButterMorph.Json.Schema;
using global::ButterMorph.SchemaDesign;
using global::ButterMorph.Web.Razor;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using NSubstitute;
using System.Reflection;
using DesignSchemaContractSnapshot = Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.SchemaContractSnapshot;

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
    public async Task TransformationHostLoadsAndSavesForwardTaskTransformation()
    {
        var parser = new OrchestrationButterMorphDesignerContextParser();
        var schemaContextService = Substitute.For<IOrchestrationSchemaContextApplicationService>();
        var taskApplicationService = Substitute.For<ITaskApplicationService>();
        var host = new OrchestrationButterMorphDesignerHost(
            schemaContextService,
            taskApplicationService,
            Substitute.For<ITriggerBindingApplicationService>(),
            parser,
            CreateImporter(),
            new OrchestrationButterMorphSourceMetadataFactory());

        schemaContextService.GetForTask(
                Arg.Is<GetTaskSchemaContextQuery>(x =>
                    x.OrchestrationVersionId == "version-1" &&
                    x.TaskDefinitionId == "task-1"))
            .Returns(CreateSchemaContextWithBindings());
        taskApplicationService.GetById(Arg.Is<GetTaskDefinitionByIdQuery>(x => x.Id == "task-1"))
            .Returns(new TaskDefinitionModel
            {
                Id = "task-1",
                Transformation = CreateTransformation("map existing")
            });
        taskApplicationService.SetTransformation(Arg.Any<SetTaskTransformationCommand>())
            .Returns(true);

        var load = await host.Load(new ButterMorphDesignerLoadRequest
        {
            ContextKey = parser.FormatTaskTransformation("version-1", "task-1")
        });
        var save = await host.Save(new ButterMorphDesignerSaveRequest
        {
            ContextKey = parser.FormatTaskTransformation("version-1", "task-1"),
            DslContent = "map updated"
        });

        Assert.Empty(load.Message);
        Assert.Equal("map existing", load.InitialDslContent);
        Assert.False(load.ShowSchemaActions);
        Assert.NotNull(load.TargetSchema);
        Assert.Contains("trigger", load.SourceSchemas.Keys);
        Assert.Contains("trigger", load.SourceMetadata.Keys);
        Assert.True(save.Succeeded);
        Assert.Equal("Transformation saved.", save.Message);
        await taskApplicationService.Received(1).SetTransformation(
            Arg.Is<SetTaskTransformationCommand>(x =>
                MatchesTransformation(x, "task-1", "map updated", "schema-signature", "target-hash")));
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
    public async Task TransformationHostLoadsCompensationTransformationsAndRejectsInvalidContexts()
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
            CreateImporter(),
            new OrchestrationButterMorphSourceMetadataFactory());

        schemaContextService.GetForTaskCompensation(Arg.Any<GetTaskCompensationSchemaContextQuery>())
            .Returns(CreateSchemaContextWithBindings());
        schemaContextService.GetForTriggerCompensation(Arg.Any<GetTriggerCompensationSchemaContextQuery>())
            .Returns(CreateSchemaContextWithBindings());
        taskApplicationService.GetById(Arg.Any<GetTaskDefinitionByIdQuery>())
            .Returns(new TaskDefinitionModel
            {
                Id = "task-1",
                CompensationDefinition = new CompensationDefinition
                {
                    HasTransformation = true,
                    Transformation = CreateTransformation("map task compensation existing")
                }
            });
        triggerBindingApplicationService.GetById(Arg.Any<GetTriggerBindingByIdQuery>())
            .Returns(new TriggerBindingModel
            {
                Id = "trigger-1",
                CompensationDefinition = new CompensationDefinition
                {
                    HasTransformation = true,
                    Transformation = CreateTransformation("map trigger compensation existing")
                }
            });

        var taskLoad = await host.Load(new ButterMorphDesignerLoadRequest
        {
            ContextKey = parser.FormatTaskCompensationTransformation("version-1", "task-1")
        });
        var triggerLoad = await host.Load(new ButterMorphDesignerLoadRequest
        {
            ContextKey = parser.FormatTriggerCompensationTransformation("version-1", "trigger-1")
        });
        var invalidLoad = await host.Load(new ButterMorphDesignerLoadRequest { ContextKey = "invalid" });
        var invalidSave = await host.Save(new ButterMorphDesignerSaveRequest { ContextKey = "invalid" });

        Assert.Equal("map task compensation existing", taskLoad.InitialDslContent);
        Assert.Equal("map trigger compensation existing", triggerLoad.InitialDslContent);
        Assert.NotNull(taskLoad.TargetSchema);
        Assert.NotNull(triggerLoad.TargetSchema);
        Assert.Contains("Invalid orchestration transformation context.", invalidLoad.Message);
        Assert.False(invalidSave.Succeeded);
        Assert.Contains("Invalid orchestration transformation context.", invalidSave.Message);
    }

    [Fact]
    public async Task TransformationHostReportsDiagnosticsForMissingSnapshotsAndImportFailures()
    {
        var parser = new OrchestrationButterMorphDesignerContextParser();
        var schemaContextService = Substitute.For<IOrchestrationSchemaContextApplicationService>();
        var taskApplicationService = Substitute.For<ITaskApplicationService>();
        var host = new OrchestrationButterMorphDesignerHost(
            schemaContextService,
            taskApplicationService,
            Substitute.For<ITriggerBindingApplicationService>(),
            parser,
            new SelectiveSchemaImporter(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["events.sales.reply"] = "source unavailable",
                ["commands.sales.reserve"] = "target unavailable"
            }),
            new OrchestrationButterMorphSourceMetadataFactory());

        schemaContextService.GetForTask(Arg.Any<GetTaskSchemaContextQuery>())
            .Returns(
                CreateSchemaContextWithoutTargetBinding(),
                CreateSchemaContextWithMissingSnapshots(),
                CreateSchemaContextWithImportFailures());
        taskApplicationService.GetById(Arg.Any<GetTaskDefinitionByIdQuery>())
            .Returns(new TaskDefinitionModel { Id = "task-1" });

        var noTarget = await host.Load(new ButterMorphDesignerLoadRequest
        {
            ContextKey = parser.FormatTaskTransformation("version-1", "task-1")
        });
        var missingSnapshots = await host.Load(new ButterMorphDesignerLoadRequest
        {
            ContextKey = parser.FormatTaskTransformation("version-1", "task-1")
        });
        var importFailures = await host.Load(new ButterMorphDesignerLoadRequest
        {
            ContextKey = parser.FormatTaskTransformation("version-1", "task-1")
        });

        Assert.Null(noTarget.TargetSchema);
        Assert.Contains("The current task does not define a request schema target.", noTarget.Message);
        Assert.Contains("The current task does not have a usable request schema target.", noTarget.Message);
        Assert.Null(missingSnapshots.TargetSchema);
        Assert.Contains("Target 'request' does not have a stored schema snapshot", missingSnapshots.Message);
        Assert.Contains("Source 'metadata' does not have a stored schema snapshot for unknown contract", missingSnapshots.Message);
        Assert.Contains("Source 'previous' does not have a stored schema snapshot", missingSnapshots.Message);
        Assert.Null(importFailures.TargetSchema);
        Assert.Empty(importFailures.SourceSchemas);
        Assert.Contains("reply: source unavailable", importFailures.Message);
        Assert.Contains("request: target unavailable", importFailures.Message);
        Assert.Contains("The current task does not have a usable request schema target.", importFailures.Message);
    }

    [Fact]
    public async Task TransformationHostHandlesLoadAndSaveExceptions()
    {
        var parser = new OrchestrationButterMorphDesignerContextParser();
        var schemaContextService = Substitute.For<IOrchestrationSchemaContextApplicationService>();
        var host = new OrchestrationButterMorphDesignerHost(
            schemaContextService,
            Substitute.For<ITaskApplicationService>(),
            Substitute.For<ITriggerBindingApplicationService>(),
            parser,
            CreateImporter(),
            new OrchestrationButterMorphSourceMetadataFactory());

        schemaContextService.GetForTask(Arg.Any<GetTaskSchemaContextQuery>())
            .Returns(Task.FromException<OrchestrationSchemaContext>(new InvalidOperationException("schema offline")));

        var load = await host.Load(new ButterMorphDesignerLoadRequest
        {
            ContextKey = parser.FormatTaskTransformation("version-1", "task-1")
        });
        var save = await host.Save(new ButterMorphDesignerSaveRequest
        {
            ContextKey = parser.FormatTaskTransformation("version-1", "task-1"),
            DslContent = "map updated"
        });

        Assert.Equal("schema offline", load.Message);
        Assert.False(save.Succeeded);
        Assert.Equal("schema offline", save.Message);
    }

    [Fact]
    public async Task TransformationHostSavesBlankDslAsClearedTransformation()
    {
        var parser = new OrchestrationButterMorphDesignerContextParser();
        var schemaContextService = Substitute.For<IOrchestrationSchemaContextApplicationService>();
        var taskApplicationService = Substitute.For<ITaskApplicationService>();
        var host = new OrchestrationButterMorphDesignerHost(
            schemaContextService,
            taskApplicationService,
            Substitute.For<ITriggerBindingApplicationService>(),
            parser,
            CreateImporter(),
            new OrchestrationButterMorphSourceMetadataFactory());

        schemaContextService.GetForTask(Arg.Any<GetTaskSchemaContextQuery>())
            .Returns(CreateSchemaContextWithBindings());
        SetTaskTransformationCommand? captured = null;
        taskApplicationService.SetTransformation(Arg.Do<SetTaskTransformationCommand>(x => captured = x))
            .Returns(false);

        var result = await host.Save(new ButterMorphDesignerSaveRequest
        {
            ContextKey = parser.FormatTaskTransformation("version-1", "task-1"),
            DslContent = " "
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Transformation could not be saved.", result.Message);
        Assert.NotNull(captured);
        Assert.Equal("task-1", captured!.Id);
        Assert.Null(captured.Transformation);
    }

    [Fact]
    public void TransformationHostBuildsLoadFailureMessages()
    {
        var method = typeof(OrchestrationButterMorphDesignerHost).GetMethod(
            "BuildLoadFailureMessage",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        Assert.Equal(
            "ButterMorph could not load the orchestration schema context.",
            method.Invoke(null, [Array.Empty<string>()]));
        Assert.Equal(
            $"first{Environment.NewLine}second",
            method.Invoke(null, [new[] { "first", "first", "second" }]));
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

    [Fact]
    public async Task ValidationHostLoadsAndSavesStageAndTaskConditions()
    {
        var parser = new OrchestrationButterMorphDesignerContextParser();
        var schemaContextService = Substitute.For<IOrchestrationSchemaContextApplicationService>();
        var stageApplicationService = Substitute.For<IStageApplicationService>();
        var taskApplicationService = Substitute.For<ITaskApplicationService>();
        var host = new OrchestrationButterMorphValidationDesignerHost(
            schemaContextService,
            stageApplicationService,
            taskApplicationService,
            Substitute.For<ITriggerBindingApplicationService>(),
            parser,
            CreateImporter(),
            new OrchestrationButterMorphSourceMetadataFactory());

        schemaContextService.GetForStage(Arg.Any<GetStageSchemaContextQuery>())
            .Returns(CreateSchemaContextWithBindings(includeTarget: false));
        schemaContextService.GetForTask(Arg.Any<GetTaskSchemaContextQuery>())
            .Returns(CreateSchemaContextWithBindings(includeTarget: false));
        stageApplicationService.GetById(Arg.Any<GetStageDefinitionByIdQuery>())
            .Returns(new StageDefinitionModel
            {
                Id = "stage-1",
                HasExecutionCondition = true,
                ExecutionCondition = CreateCondition("stage.ready == true")
            });
        taskApplicationService.GetById(Arg.Any<GetTaskDefinitionByIdQuery>())
            .Returns(new TaskDefinitionModel
            {
                Id = "task-1",
                HasExecutionCondition = true,
                ExecutionCondition = CreateCondition("task.input != null")
            });
        stageApplicationService.SetExecutionCondition(Arg.Any<SetStageExecutionConditionCommand>())
            .Returns(true);
        taskApplicationService.SetExecutionCondition(Arg.Any<SetTaskExecutionConditionCommand>())
            .Returns(true);

        var stageLoad = await host.Load(new ButterMorphValidationDesignerLoadRequest
        {
            ContextKey = parser.FormatStageExecutionCondition("version-1", "stage-1")
        });
        var taskLoad = await host.Load(new ButterMorphValidationDesignerLoadRequest
        {
            ContextKey = parser.FormatTaskExecutionCondition("version-1", "task-1")
        });
        var stageSave = await host.Save(new ButterMorphValidationDesignerSaveRequest
        {
            ContextKey = parser.FormatStageExecutionCondition("version-1", "stage-1"),
            DslContent = "stage.saved"
        });
        var taskSave = await host.Save(new ButterMorphValidationDesignerSaveRequest
        {
            ContextKey = parser.FormatTaskExecutionCondition("version-1", "task-1"),
            DslContent = "task.saved"
        });

        Assert.Equal("stage.ready == true", stageLoad.InitialDslContent);
        Assert.Equal("task.input != null", taskLoad.InitialDslContent);
        Assert.Contains("trigger", stageLoad.SourceSchemas.Keys);
        Assert.Contains("trigger", taskLoad.SourceMetadata.Keys);
        Assert.True(stageSave.Succeeded);
        Assert.True(taskSave.Succeeded);
        await stageApplicationService.Received(1).SetExecutionCondition(
            Arg.Is<SetStageExecutionConditionCommand>(x => MatchesCondition(x, "stage-1", "stage.saved")));
        await taskApplicationService.Received(1).SetExecutionCondition(
            Arg.Is<SetTaskExecutionConditionCommand>(x => MatchesCondition(x, "task-1", "task.saved")));
    }

    [Fact]
    public async Task ValidationHostLoadsCompensationConditionsAndRejectsInvalidContexts()
    {
        var parser = new OrchestrationButterMorphDesignerContextParser();
        var schemaContextService = Substitute.For<IOrchestrationSchemaContextApplicationService>();
        var taskApplicationService = Substitute.For<ITaskApplicationService>();
        var triggerBindingApplicationService = Substitute.For<ITriggerBindingApplicationService>();
        var host = new OrchestrationButterMorphValidationDesignerHost(
            schemaContextService,
            Substitute.For<IStageApplicationService>(),
            taskApplicationService,
            triggerBindingApplicationService,
            parser,
            CreateImporter(),
            new OrchestrationButterMorphSourceMetadataFactory());

        schemaContextService.GetForTaskCompensation(Arg.Any<GetTaskCompensationSchemaContextQuery>())
            .Returns(CreateSchemaContextWithBindings(includeTarget: false));
        schemaContextService.GetForTriggerCompensation(Arg.Any<GetTriggerCompensationSchemaContextQuery>())
            .Returns(CreateSchemaContextWithBindings(includeTarget: false));
        taskApplicationService.GetById(Arg.Any<GetTaskDefinitionByIdQuery>())
            .Returns(new TaskDefinitionModel
            {
                Id = "task-1",
                CompensationDefinition = new CompensationDefinition
                {
                    HasExecutionCondition = true,
                    ExecutionCondition = CreateCondition("task.compensate == true")
                }
            });
        triggerBindingApplicationService.GetById(Arg.Any<GetTriggerBindingByIdQuery>())
            .Returns(new TriggerBindingModel
            {
                Id = "trigger-1",
                CompensationDefinition = new CompensationDefinition
                {
                    HasExecutionCondition = true,
                    ExecutionCondition = CreateCondition("trigger.compensate == true")
                }
            });

        var taskLoad = await host.Load(new ButterMorphValidationDesignerLoadRequest
        {
            ContextKey = parser.FormatTaskCompensationExecutionCondition("version-1", "task-1")
        });
        var triggerLoad = await host.Load(new ButterMorphValidationDesignerLoadRequest
        {
            ContextKey = parser.FormatTriggerCompensationExecutionCondition("version-1", "trigger-1")
        });
        var invalidLoad = await host.Load(new ButterMorphValidationDesignerLoadRequest { ContextKey = "invalid" });
        var invalidSave = await host.Save(new ButterMorphValidationDesignerSaveRequest { ContextKey = "invalid" });

        Assert.Equal("task.compensate == true", taskLoad.InitialDslContent);
        Assert.Equal("trigger.compensate == true", triggerLoad.InitialDslContent);
        Assert.Contains("trigger", taskLoad.SourceSchemas.Keys);
        Assert.Contains("Invalid orchestration execution condition context.", invalidLoad.Message);
        Assert.False(invalidSave.Succeeded);
        Assert.Contains("Invalid orchestration execution condition context.", invalidSave.Message);
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

    private static OrchestrationSchemaContext CreateSchemaContextWithBindings(bool includeTarget = true)
        => new()
        {
            OrchestrationVersionId = "version-1",
            OrchestrationVersion = "1.0.0",
            StageKey = "stage-1",
            TaskKey = "task-1",
            Signature = "schema-signature",
            Sources =
            [
                new OrchestrationSchemaSource
                {
                    Alias = "trigger",
                    SourceKind = OrchestrationSchemaContextSourceKind.Trigger,
                    SchemaBinding = CreateBinding("events.sales.sale.created", "source-hash")
                }
            ],
            Target = includeTarget
                ? new OrchestrationSchemaTarget
                {
                    Alias = "request",
                    StageKey = "stage-1",
                    TaskKey = "task-1",
                    SchemaBinding = CreateBinding("commands.sales.reserve", "target-hash")
                }
                : null
        };

    private static OrchestrationSchemaContext CreateSchemaContextWithoutTargetBinding()
        => new()
        {
            OrchestrationVersionId = "version-1",
            OrchestrationVersion = "1.0.0",
            StageKey = "stage-1",
            TaskKey = "task-1",
            Signature = "schema-signature",
            Sources = [],
            Target = new OrchestrationSchemaTarget
            {
                Alias = "request",
                StageKey = "stage-1",
                TaskKey = "task-1"
            }
        };

    private static OrchestrationSchemaContext CreateSchemaContextWithMissingSnapshots()
    {
        var targetBinding = CreateBinding("commands.sales.reserve", "target-hash");
        targetBinding.Snapshot = null;
        var sourceBinding = CreateBinding("events.sales.reply", "source-hash");
        sourceBinding.Snapshot = null;

        return new OrchestrationSchemaContext
        {
            OrchestrationVersionId = "version-1",
            OrchestrationVersion = "1.0.0",
            StageKey = "stage-1",
            TaskKey = "task-1",
            Signature = "schema-signature",
            Sources =
            [
                new OrchestrationSchemaSource
                {
                    Alias = "metadata",
                    SourceKind = OrchestrationSchemaContextSourceKind.Metadata
                },
                new OrchestrationSchemaSource
                {
                    Alias = "previous",
                    SourceKind = OrchestrationSchemaContextSourceKind.TaskResponse,
                    StageKey = "stage-1",
                    TaskKey = "task-previous",
                    SchemaBinding = sourceBinding
                }
            ],
            Target = new OrchestrationSchemaTarget
            {
                Alias = "request",
                StageKey = "stage-1",
                TaskKey = "task-1",
                SchemaBinding = targetBinding
            }
        };
    }

    private static OrchestrationSchemaContext CreateSchemaContextWithImportFailures()
        => new()
        {
            OrchestrationVersionId = "version-1",
            OrchestrationVersion = "1.0.0",
            StageKey = "stage-1",
            TaskKey = "task-1",
            Signature = "schema-signature",
            Sources =
            [
                new OrchestrationSchemaSource
                {
                    Alias = "reply",
                    SourceKind = OrchestrationSchemaContextSourceKind.TaskResponse,
                    StageKey = "stage-1",
                    TaskKey = "task-previous",
                    SchemaBinding = CreateBinding("events.sales.reply", "source-hash")
                }
            ],
            Target = new OrchestrationSchemaTarget
            {
                Alias = "request",
                StageKey = "stage-1",
                TaskKey = "task-1",
                SchemaBinding = CreateBinding("commands.sales.reserve", "target-hash")
            }
        };

    private static OrchestrationButterMorphSchemaImporter CreateImporter()
        => new(
            new JsonSchemaImporter(),
            new PayloadSchemaDefinitionHydrator(),
            new PayloadSchemaBuilder());

    private static SchemaBinding CreateBinding(string contractKey, string contentHash)
        => new()
        {
            Id = Id.New(),
            ElementType = ElementType.Task,
            ElementId = Id.New(),
            ContractId = Id.New(),
            ContractKey = contractKey,
            ContractVersion = new SemanticVersion(1, 0, 0),
            RegistryProviderId = Id.New(),
            RegistryProviderKey = "knowl",
            ContractKind = SchemaContractKind.CommandRequest,
            Snapshot = new DesignSchemaContractSnapshot
            {
                ContractKind = SchemaContractKind.CommandRequest,
                RegistryProviderKey = "knowl",
                ContractId = $"knowl:{contractKey}",
                ContractKey = contractKey,
                ContractVersion = "1.0.0",
                SchemaFormat = "JsonSchema",
                SchemaJson = $$"""
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "title": "{{contractKey}}",
                  "type": "object",
                  "properties": {
                    "id": {
                      "type": "string"
                    }
                  },
                  "required": [ "id" ]
                }
                """,
                ContentHash = contentHash
            }
        };

    private static TransformationDefinition CreateTransformation(string dsl)
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration
            {
                Dsl = dsl,
                SourceContextHash = "schema-signature",
                TargetSchemaHash = "target-hash",
                SemanticDiagnosticsJson = "{}"
            }
        };

    private static ExecutionCondition CreateCondition(string dsl)
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration
            {
                Expression = new Expression(dsl)
            }
        };

    private static bool MatchesTransformation(
        SetTaskTransformationCommand command,
        string id,
        string dsl,
        string sourceContextHash,
        string targetSchemaHash)
        => command.Id == id &&
            command.Transformation.Configuration is DslTransformationConfiguration configuration &&
            configuration.Dsl == dsl &&
            configuration.SourceContextHash == sourceContextHash &&
            configuration.TargetSchemaHash == targetSchemaHash;

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
        SetStageExecutionConditionCommand command,
        string id,
        string dsl)
        => command.Id == id &&
            command.ExecutionCondition.Configuration is DslConditionConfiguration configuration &&
            configuration.Expression.ToString() == dsl;

    private static bool MatchesCondition(
        SetTaskExecutionConditionCommand command,
        string id,
        string dsl)
        => command.Id == id &&
            command.ExecutionCondition.Configuration is DslConditionConfiguration configuration &&
            configuration.Expression.ToString() == dsl;

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

    private sealed class SelectiveSchemaImporter : IOrchestrationButterMorphSchemaImporter
    {
        private readonly IReadOnlyDictionary<string, string> _failures;

        public SelectiveSchemaImporter(IReadOnlyDictionary<string, string> failures)
            => _failures = failures;

        public bool TryImport(
            SchemaBinding binding,
            out global::ButterMorph.Abstractions.IStructureSchema schema,
            out string message)
        {
            if (_failures.TryGetValue(binding.ContractKey, out var failureMessage))
            {
                schema = null!;
                message = failureMessage;
                return false;
            }

            schema = Substitute.For<global::ButterMorph.Abstractions.IStructureSchema>();
            message = string.Empty;
            return true;
        }
    }
}
