using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class RuntimeStageSummaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadsOnlyBoundedRecordedStagesForSelectedInstance(bool relational)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        if (relational) await connection.OpenAsync();
        var commands = new List<string>();
        var services = new ServiceCollection();
        services.AddLogging(); services.AddKrackendOrchestrationsRuntime();
        if (relational) services.AddOrchestratorRuntimeStorageEntityFramework(options => options.UseSqlite(connection)
            .LogTo(commands.Add, [Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted]));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        if (relational) await scope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IStageExecutionRepository>();
        var id = Id.New(); var other = Id.New();
        for (var i = 0; i < 23; i++)
            await repository.Create(new StageExecution { Id = Id.New(), OrchestrationInstanceId = id, StageKey = $"stage-{i:D2}", Order = i, Status = StageExecutionStatus.Completed });
        await repository.Create(new StageExecution { Id = Id.New(), OrchestrationInstanceId = other, StageKey = "other", Order = 0, Status = StageExecutionStatus.Running });
        commands.Clear();
        var reader = Assert.IsAssignableFrom<IRuntimeStageSummaryReader>(repository);
        var result = await reader.GetStageSummaries(id, 20);
        Assert.Equal(20, result.Items.Count); Assert.True(result.HasMore);
        Assert.Equal("stage-00", result.Items.First().StageKey); Assert.Equal("stage-19", result.Items.Last().StageKey);
        Assert.DoesNotContain(result.Items, item => item.StageKey == "other");
        if (relational)
        {
            Assert.Single(commands);
            Assert.DoesNotContain(commands, command => command.Contains("Metadata", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(commands, command => command.Contains("Artifact", StringComparison.OrdinalIgnoreCase));
        }
        var empty = await reader.GetStageSummaries(Id.New());
        Assert.Empty(empty.Items); Assert.False(empty.HasMore);
    }
}
