using System.Data.Common;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Mongo;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class RuntimeDetailBatchReadTests
{
    [Fact(Timeout = 180_000)]
    public async Task BatchReadsTranslateAndReturnMatchesInSqlServer()
    {
        const string password = "Krackend_12345!";
        await using var sql = new ContainerBuilder().WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithEnvironment("ACCEPT_EULA", "Y").WithEnvironment("MSSQL_SA_PASSWORD", password)
            .WithPortBinding(1433, true).WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(1433)).Build();
        await sql.StartAsync();
        var connection = $"Server=localhost,{sql.GetMappedPublicPort(1433)};Database=DetailBatch;User Id=sa;Password={password};TrustServerCertificate=True;Encrypt=False;Connect Timeout=2";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.AddOrchestratorRuntimeStorageEntityFramework(options => options.UseSqlServer(connection));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        for (var i = 0; ; i++)
        {
            try { await db.Database.EnsureCreatedAsync(); break; }
            catch (Microsoft.Data.SqlClient.SqlException) when (i < 30) { await Task.Delay(1000); }
        }
        var taskIds = Enumerable.Range(0, 3).Select(_ => Id.New()).ToArray();
        var attempts = taskIds.Select(id => new TaskExecutionAttempt { Id = Id.New(), TaskExecutionId = id, AttemptNumber = 1, RequestPayload = JsonNode.Parse("""{"large":true}""") }).ToArray();
        var dispatches = attempts.Select(a => new TaskDispatch { Id = Id.New(), TaskExecutionAttemptId = a.Id,
            DispatchType = "Messaging", DispatchStatus = "Sent", CommandId = Guid.NewGuid().ToString() }).ToArray();
        db.TaskExecutionAttempts.AddRange(attempts);
        db.TaskDispatches.AddRange(dispatches);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var attemptReader = Assert.IsAssignableFrom<ITaskExecutionAttemptBatchReader>(scope.ServiceProvider.GetRequiredService<ITaskExecutionAttemptRepository>());
        var dispatchReader = Assert.IsAssignableFrom<ITaskDispatchBatchReader>(scope.ServiceProvider.GetRequiredService<ITaskDispatchRepository>());
        Assert.Equal(3, (await attemptReader.GetByTaskExecutionIds(taskIds)).Count);
        var summaryReader = Assert.IsAssignableFrom<ITaskExecutionAttemptSummaryBatchReader>(attemptReader);
        var summaries = await summaryReader.GetSummariesByTaskExecutionIds(taskIds);
        Assert.Equal(3, summaries.Count);
        Assert.All(summaries, item => Assert.Null(item.RequestPayload));
        Assert.Equal(3, (await dispatchReader.GetByIds(dispatches.Select(x => x.Id).ToArray())).Count);
        Assert.Equal(3, (await dispatchReader.GetByAttemptIds(attempts.Select(x => x.Id).ToArray())).Count);
    }

    [Fact(Timeout = 120_000)]
    public async Task BatchReadsTranslateAndReturnMatchesInMongo()
    {
        await using var mongo = new ContainerBuilder().WithImage("mongo:8.0.4").WithPortBinding(27017, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(27017)).Build();
        await mongo.StartAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.AddOrchestratorRuntimeStorageEntityFramework(options =>
            options.UseMongoDB($"mongodb://{mongo.Hostname}:{mongo.GetMappedPublicPort(27017)}", "detail_batch_test"),
            storage => storage.ConfigureModel = model => model.UseKrackendMongoRuntimeModel());
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        db.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        await db.Database.EnsureCreatedAsync();
        var taskIds = Enumerable.Range(0, 3).Select(_ => Id.New()).ToArray();
        var attempts = taskIds.Select(id => new TaskExecutionAttempt { Id = Id.New(), TaskExecutionId = id, AttemptNumber = 1, RequestPayload = JsonNode.Parse("""{"large":true}""") }).ToArray();
        var dispatches = attempts.Select(a => new TaskDispatch { Id = Id.New(), TaskExecutionAttemptId = a.Id,
            DispatchType = "Messaging", DispatchStatus = "Sent", CommandId = Guid.NewGuid().ToString() }).ToArray();
        db.TaskExecutionAttempts.AddRange(attempts);
        db.TaskDispatches.AddRange(dispatches);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var attemptReader = Assert.IsAssignableFrom<ITaskExecutionAttemptBatchReader>(scope.ServiceProvider.GetRequiredService<ITaskExecutionAttemptRepository>());
        var dispatchReader = Assert.IsAssignableFrom<ITaskDispatchBatchReader>(scope.ServiceProvider.GetRequiredService<ITaskDispatchRepository>());
        Assert.Equal(3, (await attemptReader.GetByTaskExecutionIds(taskIds)).Count);
        var summaryReader = Assert.IsAssignableFrom<ITaskExecutionAttemptSummaryBatchReader>(attemptReader);
        var summaries = await summaryReader.GetSummariesByTaskExecutionIds(taskIds);
        Assert.Equal(3, summaries.Count);
        Assert.All(summaries, item => Assert.Null(item.RequestPayload));
        Assert.Equal(3, (await dispatchReader.GetByIds(dispatches.Select(x => x.Id).ToArray())).Count);
        Assert.Equal(3, (await dispatchReader.GetByAttemptIds(attempts.Select(x => x.Id).ToArray())).Count);
    }

    [Fact]
    public async Task BatchReadsUseBoundedQueriesAndReturnEveryMatch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var counter = new ReadCounter();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.AddOrchestratorRuntimeStorageEntityFramework(options => options.UseSqlite(connection).AddInterceptors(counter));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var taskIds = Enumerable.Range(0, 205).Select(_ => Id.New()).ToArray();
        var attempts = taskIds.Select(id => new TaskExecutionAttempt { Id = Id.New(), TaskExecutionId = id, AttemptNumber = 1 }).ToArray();
        var dispatches = attempts.Select(a => new TaskDispatch { Id = Id.New(), TaskExecutionAttemptId = a.Id,
            DispatchType = "Messaging", DispatchStatus = "Sent", CommandId = Guid.NewGuid().ToString() }).ToArray();
        db.TaskExecutionAttempts.AddRange(attempts);
        db.TaskDispatches.AddRange(dispatches);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        counter.Count = 0;

        var attemptReader = Assert.IsAssignableFrom<ITaskExecutionAttemptBatchReader>(scope.ServiceProvider.GetRequiredService<ITaskExecutionAttemptRepository>());
        var dispatchReader = Assert.IsAssignableFrom<ITaskDispatchBatchReader>(scope.ServiceProvider.GetRequiredService<ITaskDispatchRepository>());
        var loadedAttempts = await attemptReader.GetByTaskExecutionIds([.. taskIds, taskIds[0]]);
        var loadedById = await dispatchReader.GetByIds(dispatches.Select(x => x.Id).ToArray());
        var loadedByAttempt = await dispatchReader.GetByAttemptIds(attempts.Select(x => x.Id).ToArray());

        Assert.Equal(205, loadedAttempts.Count);
        Assert.Equal(205, loadedById.Count);
        Assert.Equal(205, loadedByAttempt.Count);
        Assert.Equal(6, counter.Count);

        counter.Count = 0;
        counter.Commands.Clear();
        var summaries = await Assert.IsAssignableFrom<ITaskExecutionAttemptSummaryBatchReader>(attemptReader)
            .GetSummariesByTaskExecutionIds(taskIds);
        Assert.Equal(205, summaries.Count);
        Assert.Equal(2, counter.Count);
        Assert.All(counter.Commands, sql => Assert.DoesNotContain("RequestPayload", sql, StringComparison.OrdinalIgnoreCase));
        Assert.All(counter.Commands, sql => Assert.DoesNotContain("ResponsePayload", sql, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class ReadCounter : DbCommandInterceptor
    {
        public int Count { get; set; }
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Count++;
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
