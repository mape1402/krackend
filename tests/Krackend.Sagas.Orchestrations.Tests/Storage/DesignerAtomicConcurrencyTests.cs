namespace Krackend.Sagas.Orchestrations.Tests.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Repositories;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed class DesignerAtomicConcurrencyTests
{
    [Fact]
    public async Task ExistingJsonFormattingDoesNotCauseFalseConflict()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>().UseSqlite(connection).Options;
        var taskId = Id.New();
        await using (var setup = new ControlPlaneDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.TaskDefinitions.Add(new TaskDefinitionEntity
            {
                Id = taskId, StageDefinitionId = Id.New(), Key = "task", Name = "Task", Kind = TaskKind.Messaging,
                ExecutionMode = TaskExecutionMode.Sequential, DispatchType = TaskDispatchType.FireAndWaitCallback
            });
            await setup.SaveChangesAsync();
            var initial = new TransformationDefinition { Engine = EngineType.DSL, Configuration = new DslTransformationConfiguration { Dsl = "initial" } };
            await new TaskRepository(setup).SetTransformation(taskId, initial);
        }
        await using (var read = connection.CreateCommand())
        {
            read.CommandText = "SELECT TransformationJson FROM TaskDefinitions LIMIT 1";
            var original = (string)(await read.ExecuteScalarAsync())!;
            var formatted = JsonNode.Parse(original)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            await using var update = connection.CreateCommand();
            update.CommandText = "UPDATE TaskDefinitions SET TransformationJson = $json";
            update.Parameters.AddWithValue("$json", formatted);
            await update.ExecuteNonQueryAsync();
        }
        await using var context = new ControlPlaneDbContext(options);
        await Write(context, "task-transform", "updated", OrchestrationDesignerRevision.FromDsl("initial"), taskId, Id.New(), Id.New());
        await using var verify = new ControlPlaneDbContext(options);
        Assert.Equal("updated", await Read(verify, "task-transform", taskId, Id.New(), Id.New()));
    }

    [Theory]
    [InlineData("task-transform")]
    [InlineData("task-condition")]
    [InlineData("task-compensation-transform")]
    [InlineData("task-compensation-condition")]
    [InlineData("stage-condition")]
    [InlineData("trigger-compensation-transform")]
    [InlineData("trigger-compensation-condition")]
    public async Task ConcurrentEditorCannotOverwriteCommittedDesignerDocument(string kind)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"designer-concurrency-{Guid.NewGuid():N}", Mode = SqliteOpenMode.Memory, Cache = SqliteCacheMode.Shared
        }.ToString();
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var baseOptions = new DbContextOptionsBuilder<ControlPlaneDbContext>().UseSqlite(connectionString).Options;
        var taskId = Id.New();
        var stageId = Id.New();
        var triggerId = Id.New();
        await using (var setup = new ControlPlaneDbContext(baseOptions))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.StageDefinitions.Add(new StageDefinitionEntity { Id = stageId, OrchestrationVersionId = Id.New(), Key = "stage", Name = "Stage" });
            setup.TaskDefinitions.Add(new TaskDefinitionEntity
            {
                Id = taskId, StageDefinitionId = stageId, Key = "task", Name = "Task", Kind = TaskKind.Messaging,
                ExecutionMode = TaskExecutionMode.Sequential, DispatchType = TaskDispatchType.FireAndWaitCallback
            });
            setup.TriggerBindings.Add(new TriggerBindingEntity { Id = triggerId, OrchestrationVersionId = Id.New(), Key = "trigger" });
            await setup.SaveChangesAsync();
        }

        var gate = new SaveGate();
        var waitingOptions = new DbContextOptionsBuilder<ControlPlaneDbContext>().UseSqlite(connectionString).AddInterceptors(gate).Options;
        await using var first = new ControlPlaneDbContext(baseOptions);
        await using var second = new ControlPlaneDbContext(waitingOptions);
        var expected = OrchestrationDesignerRevision.FromDsl(string.Empty);
        var delayedSave = Write(second, kind, "second", expected, taskId, stageId, triggerId);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await Write(first, kind, "first", expected, taskId, stageId, triggerId);
        }
        finally
        {
            gate.Release.TrySetResult();
        }
        await Assert.ThrowsAsync<OrchestrationDesignerConcurrencyException>(() => delayedSave);

        await using var verify = new ControlPlaneDbContext(baseOptions);
        Assert.Equal("first", await Read(verify, kind, taskId, stageId, triggerId));
    }

    internal static Task Write(ControlPlaneDbContext context, string kind, string dsl, string expected, Id taskId, Id stageId, Id triggerId)
    {
        var transformation = new TransformationDefinition { Engine = EngineType.DSL, Configuration = new DslTransformationConfiguration { Dsl = dsl } };
        var condition = new ExecutionCondition { Engine = EngineType.DSL, Configuration = new DslConditionConfiguration { Expression = new Expression(dsl) } };
        var tasks = new TaskRepository(context);
        var stages = new StageRepository(context);
        var triggers = new TriggerBindingRepository(context);
        return kind switch
        {
            "task-transform" => tasks.SetTransformationIfUnchanged(taskId, transformation, expected),
            "task-condition" => tasks.SetExecutionConditionIfUnchanged(taskId, condition, expected),
            "task-compensation-transform" => tasks.SetCompensationTransformationIfUnchanged(taskId, transformation, expected),
            "task-compensation-condition" => tasks.SetCompensationExecutionConditionIfUnchanged(taskId, condition, expected),
            "stage-condition" => stages.SetExecutionConditionIfUnchanged(stageId, condition, expected),
            "trigger-compensation-transform" => triggers.SetCompensationTransformationIfUnchanged(triggerId, transformation, expected),
            "trigger-compensation-condition" => triggers.SetCompensationExecutionConditionIfUnchanged(triggerId, condition, expected),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    internal static async Task<string> Read(ControlPlaneDbContext context, string kind, Id taskId, Id stageId, Id triggerId)
    {
        if (kind == "stage-condition")
            return ((DslConditionConfiguration)(await new StageRepository(context).GetById(stageId)).ExecutionCondition.Configuration).Expression.ToString();
        if (kind.StartsWith("trigger", StringComparison.Ordinal))
        {
            var compensation = (await new TriggerBindingRepository(context).GetById(triggerId)).CompensationDefinition;
            return kind.EndsWith("transform", StringComparison.Ordinal)
                ? ((DslTransformationConfiguration)compensation.Transformation.Configuration).Dsl
                : ((DslConditionConfiguration)compensation.ExecutionCondition.Configuration).Expression.ToString();
        }
        var task = await new TaskRepository(context).GetById(taskId);
        return kind switch
        {
            "task-transform" => ((DslTransformationConfiguration)task.Transformation.Configuration).Dsl,
            "task-condition" => ((DslConditionConfiguration)task.ExecutionCondition.Configuration).Expression.ToString(),
            "task-compensation-transform" => ((DslTransformationConfiguration)task.CompensationDefinition.Transformation.Configuration).Dsl,
            _ => ((DslConditionConfiguration)task.CompensationDefinition.ExecutionCondition.Configuration).Expression.ToString()
        };
    }

    internal sealed class SaveGate : SaveChangesInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }
}
