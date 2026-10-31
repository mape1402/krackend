namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mule;

public sealed class OrchestrationDurableActionStateReaderTests
{
    [Fact]
    public async Task ReadsPersistedActiveAndTerminalActionsWithoutChangingThem()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOrchestratorRuntimeStorageEntityFramework(options => options.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var pending = NewAction(DurableActionStatus.Pending);
        var locked = NewAction(DurableActionStatus.Locked);
        var completed = NewAction(DurableActionStatus.Completed);
        var failed = NewAction(DurableActionStatus.Failed);
        db.AddRange(pending, locked, completed, failed);
        await db.SaveChangesAsync();
        var reader = scope.ServiceProvider.GetRequiredService<IOrchestrationDurableActionStateReader>();
        Assert.Equal(DurableWorkPresence.Present, await reader.ReadAsync(pending.Id.ToString()));
        Assert.Equal(DurableWorkPresence.Present, await reader.ReadAsync(locked.Id.ToString()));
        Assert.Equal(DurableWorkPresence.Terminal, await reader.ReadAsync(completed.Id.ToString()));
        Assert.Equal(DurableWorkPresence.Terminal, await reader.ReadAsync(failed.Id.ToString()));
        Assert.Equal(DurableWorkPresence.Unknown, await reader.ReadAsync(global::Mule.Ulid.NewUlid().ToString()));
    }

    private static DurableAction NewAction(DurableActionStatus status) => new()
    {
        Id = global::Mule.Ulid.NewUlid(), Key = ActionKey.From("test"), Lane = "test", Payload = "{}",
        PayloadType = "test", Status = status, CreatedOnUtc = DateTimeOffset.UtcNow
    };
}
