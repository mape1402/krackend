namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Mule;
using NSubstitute;

public sealed class MuleReconciliationInspectorTests
{
    [Fact]
    public async Task OnTimeBufferedCallbackIsProtectedWhileItsPersistedStateIsUnknown()
    {
        var storage = Substitute.For<IMuleStorage>();
        var reader = Substitute.For<IOrchestrationDurableActionStateReader>();
        var inspector = new MuleOrchestrationDurableWorkInspector(storage, reader);
        var dispatch = new TaskDispatch { Id = Id.New(), TaskExecutionAttemptId = Id.New(), DispatchType = "Messaging", DispatchStatus = "WaitingResponse" };
        var attempt = new TaskExecutionAttempt { Id = dispatch.TaskExecutionAttemptId, TaskExecutionId = Id.New(), AttemptNumber = 2 };
        var action = Mule.Ulid.NewUlid();
        var deadline = DateTime.UtcNow.AddMinutes(1);
        storage.FindByDeduplicationKeyAsync(Arg.Any<ActionKey>(), $"backchannel:{dispatch.Id}:2", Arg.Any<CancellationToken>())
            .Returns((Mule.Ulid?)action);
        reader.ReadPendingBeforeAsync(action.ToString(), deadline, Arg.Any<CancellationToken>())
            .Returns(DurableWorkPresence.Unknown);

        Assert.Equal(DurableWorkPresence.Present, await inspector.InspectPendingResponseAsync(dispatch, attempt, deadline));
        await storage.DidNotReceive().FindByDeduplicationKeyAsync(Arg.Any<ActionKey>(), $"backchannel:{dispatch.Id}:0", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnTimeLegacyCallbackIsNotHiddenByALateAttemptCallback()
    {
        var storage = Substitute.For<IMuleStorage>();
        var reader = Substitute.For<IOrchestrationDurableActionStateReader>();
        var inspector = new MuleOrchestrationDurableWorkInspector(storage, reader);
        var dispatch = new TaskDispatch { Id = Id.New(), TaskExecutionAttemptId = Id.New(), DispatchType = "Messaging", DispatchStatus = "WaitingResponse" };
        var attempt = new TaskExecutionAttempt { Id = dispatch.TaskExecutionAttemptId, TaskExecutionId = Id.New(), AttemptNumber = 1 };
        var late = Mule.Ulid.NewUlid();
        var legacy = Mule.Ulid.NewUlid();
        var deadline = DateTime.UtcNow;
        storage.FindByDeduplicationKeyAsync(Arg.Any<ActionKey>(), $"backchannel:{dispatch.Id}:1", Arg.Any<CancellationToken>()).Returns((Mule.Ulid?)late);
        storage.FindByDeduplicationKeyAsync(Arg.Any<ActionKey>(), $"backchannel:{dispatch.Id}:0", Arg.Any<CancellationToken>()).Returns((Mule.Ulid?)legacy);
        reader.ReadPendingBeforeAsync(late.ToString(), deadline, Arg.Any<CancellationToken>()).Returns(DurableWorkPresence.Terminal);
        reader.ReadPendingBeforeAsync(legacy.ToString(), deadline, Arg.Any<CancellationToken>()).Returns(DurableWorkPresence.Present);

        Assert.Equal(DurableWorkPresence.Present, await inspector.InspectPendingResponseAsync(dispatch, attempt, deadline));
    }

    [Fact]
    public async Task TerminalOrLateCallbackDoesNotBlockTimeout()
    {
        var storage = Substitute.For<IMuleStorage>();
        var reader = Substitute.For<IOrchestrationDurableActionStateReader>();
        var inspector = new MuleOrchestrationDurableWorkInspector(storage, reader);
        var dispatch = new TaskDispatch { Id = Id.New(), TaskExecutionAttemptId = Id.New(), DispatchType = "Messaging", DispatchStatus = "WaitingResponse" };
        var attempt = new TaskExecutionAttempt { Id = dispatch.TaskExecutionAttemptId, TaskExecutionId = Id.New(), AttemptNumber = 1 };
        var action = Mule.Ulid.NewUlid();
        var deadline = DateTime.UtcNow.AddMinutes(-1);
        storage.FindByDeduplicationKeyAsync(Arg.Any<ActionKey>(), $"backchannel:{dispatch.Id}:1", Arg.Any<CancellationToken>())
            .Returns((Mule.Ulid?)action);
        reader.ReadPendingBeforeAsync(action.ToString(), deadline, Arg.Any<CancellationToken>())
            .Returns(DurableWorkPresence.Terminal);
        Assert.Equal(DurableWorkPresence.Terminal, await inspector.InspectPendingResponseAsync(dispatch, attempt, deadline));
    }

    [Fact]
    public async Task UnpersistedReferencedTimerDoesNotFallBackToAnOlderAction()
    {
        var storage = Substitute.For<IMuleStorage>();
        var reader = Substitute.For<IOrchestrationDurableActionStateReader>();
        var inspector = new MuleOrchestrationDurableWorkInspector(storage, reader);
        var task = new TaskExecution { Id = Id.New(), TaskKey = "task", TimeoutActionId = Id.New().ToString(), ResponseDeadlineUtc = DateTime.UtcNow.AddMinutes(1) };
        var attempt = new TaskExecutionAttempt { Id = Id.New(), TaskExecutionId = task.Id, AttemptNumber = 1 };
        reader.ReadAsync(task.TimeoutActionId, Arg.Any<CancellationToken>()).Returns(DurableWorkPresence.Unknown);
        Assert.Equal(DurableWorkPresence.Unknown, await inspector.InspectTimeoutAsync(task, attempt));
        await storage.DidNotReceiveWithAnyArgs().FindByDeduplicationKeyAsync(default, default!);
    }
}
