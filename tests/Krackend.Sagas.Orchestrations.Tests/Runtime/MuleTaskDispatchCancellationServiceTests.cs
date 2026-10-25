namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Mule;
using NSubstitute;
using MuleUlid = Mule.Ulid;

public sealed class MuleTaskDispatchCancellationServiceTests
{
    [Theory]
    [InlineData(MuleCancellationOutcome.Cancelled, true, "Cancelled")]
    [InlineData(MuleCancellationOutcome.AlreadyCancelled, true, "AlreadyCancelled")]
    [InlineData(MuleCancellationOutcome.TooLate, true, "TooLate")]
    [InlineData(MuleCancellationOutcome.NotFound, true, "NotFound")]
    [InlineData(MuleCancellationOutcome.NotSupported, true, "NotSupported")]
    public async Task CancelAsyncMapsTerminalMuleOutcomesWithoutLosingTheLogicalFence(
        MuleCancellationOutcome outcome,
        bool expectedTerminal,
        string expectedStatus)
    {
        var cancellationService = Substitute.For<IMuleCancellationService>();
        var actionId = MuleUlid.NewUlid();
        cancellationService
            .CancelAsync(actionId, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<MuleCancellationResult>(
                new MuleCancellationResult(actionId, outcome, null)));
        var service = new MuleTaskDispatchCancellationService(
            cancellationService,
            Substitute.For<IMuleReplayService>(),
            Substitute.For<IMuleStorage>());
        var dispatch = CreateDispatch();
        dispatch.Metadata["Mule.ActionId"] = JsonValue.Create(actionId.ToString());

        var result = await service.CancelAsync(new TaskDispatchCancellationRequest
        {
            Dispatch = dispatch,
            Reason = "terminal instance"
        });

        Assert.Equal(expectedTerminal, result.IsTerminal);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(outcome == MuleCancellationOutcome.Cancelled, result.Requested);
        await cancellationService.Received(1).CancelAsync(actionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsyncDefersWhenTheMuleActionReferenceIsNotDurableYet()
    {
        var storage = Substitute.For<IMuleStorage>();
        storage
            .FindByDeduplicationKeyAsync(Arg.Any<ActionKey>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<MuleUlid?>((MuleUlid?)null));
        var cancellationService = Substitute.For<IMuleCancellationService>();
        var service = new MuleTaskDispatchCancellationService(
            cancellationService,
            Substitute.For<IMuleReplayService>(),
            storage);

        var result = await service.CancelAsync(new TaskDispatchCancellationRequest
        {
            Dispatch = CreateDispatch(),
            Reason = "terminal instance"
        });

        Assert.False(result.IsTerminal);
        Assert.Equal("ActionReferencePending", result.Status);
        await cancellationService.DidNotReceiveWithAnyArgs().CancelAsync(default, default);
    }

    [Fact]
    public async Task CancelAsyncRecoversAnUnrecordedActionReferenceFromTheDispatchDeduplicationKey()
    {
        var actionId = MuleUlid.NewUlid();
        var storage = Substitute.For<IMuleStorage>();
        storage
            .FindByDeduplicationKeyAsync(
                Arg.Is<ActionKey>(key => key.ToString() == "RemoteCommandDispatch"),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<MuleUlid?>(actionId));
        var cancellations = Substitute.For<IMuleCancellationService>();
        cancellations
            .CancelAsync(actionId, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<MuleCancellationResult>(
                new MuleCancellationResult(actionId, MuleCancellationOutcome.Cancelled, null)));
        var service = new MuleTaskDispatchCancellationService(
            cancellations,
            Substitute.For<IMuleReplayService>(),
            storage);
        var dispatch = CreateDispatch();

        var result = await service.CancelAsync(new TaskDispatchCancellationRequest
        {
            Dispatch = dispatch,
            Reason = "terminal instance"
        });

        Assert.True(result.IsTerminal);
        Assert.Equal(actionId.ToString(), dispatch.Metadata["Mule.ActionId"]!.GetValue<string>());
        await storage.Received(1).FindByDeduplicationKeyAsync(
            Arg.Any<ActionKey>(),
            dispatch.Id.ToString(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsyncCancelsTheReservedReplayActionWhenAReplayIsPending()
    {
        var replayActionId = MuleUlid.NewUlid();
        var cancellations = Substitute.For<IMuleCancellationService>();
        cancellations
            .CancelAsync(replayActionId, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<MuleCancellationResult>(
                new MuleCancellationResult(replayActionId, MuleCancellationOutcome.Cancelled, null)));
        var service = new MuleTaskDispatchCancellationService(
            cancellations,
            Substitute.For<IMuleReplayService>(),
            Substitute.For<IMuleStorage>());
        var dispatch = CreateDispatch();
        dispatch.Metadata["Recovery.DispatchReplayProviderActionId"] = JsonValue.Create(replayActionId.ToString());

        var result = await service.CancelAsync(new TaskDispatchCancellationRequest
        {
            Dispatch = dispatch,
            Reason = "terminal instance"
        });

        Assert.True(result.IsTerminal);
        await cancellations.Received(1).CancelAsync(replayActionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsyncResolvesAReplayActionFromTheDurableReplayRequest()
    {
        var requestId = MuleUlid.NewUlid();
        var replayActionId = MuleUlid.NewUlid();
        var replayService = Substitute.For<IMuleReplayService>();
        replayService
            .GetReplayAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<MuleReplayInfo>(new MuleReplayInfo
            {
                RequestId = requestId,
                ReplayOfActionId = MuleUlid.NewUlid(),
                ReplayRootActionId = MuleUlid.NewUlid(),
                ReplayActionId = replayActionId,
                ReplaySequence = 1,
                Status = MuleReplayStatus.Scheduled,
                ActionKey = ActionKey.From("RemoteCommandDispatch"),
                RequestedOnUtc = DateTimeOffset.UtcNow,
                ScheduleOnUtc = DateTimeOffset.UtcNow
            }));
        var cancellations = Substitute.For<IMuleCancellationService>();
        cancellations
            .CancelAsync(replayActionId, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<MuleCancellationResult>(
                new MuleCancellationResult(replayActionId, MuleCancellationOutcome.Cancelled, null)));
        var service = new MuleTaskDispatchCancellationService(
            cancellations,
            replayService,
            Substitute.For<IMuleStorage>());
        var dispatch = CreateDispatch();
        dispatch.Metadata["Recovery.DispatchReplayRequestId"] = JsonValue.Create(requestId.ToString());

        var result = await service.CancelAsync(new TaskDispatchCancellationRequest
        {
            Dispatch = dispatch,
            Reason = "terminal instance"
        });

        Assert.True(result.IsTerminal);
        Assert.Equal(replayActionId.ToString(), dispatch.Metadata["Recovery.DispatchReplayProviderActionId"]!.GetValue<string>());
        await cancellations.Received(1).CancelAsync(replayActionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsyncTreatsMalformedActionMetadataAsAnUnresolvedReference()
    {
        var storage = Substitute.For<IMuleStorage>();
        storage
            .FindByDeduplicationKeyAsync(Arg.Any<ActionKey>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<MuleUlid?>((MuleUlid?)null));
        var service = new MuleTaskDispatchCancellationService(
            Substitute.For<IMuleCancellationService>(),
            Substitute.For<IMuleReplayService>(),
            storage);
        var dispatch = CreateDispatch();
        dispatch.Metadata["Mule.ActionId"] = JsonValue.Create(42);

        var result = await service.CancelAsync(new TaskDispatchCancellationRequest
        {
            Dispatch = dispatch,
            Reason = "terminal instance"
        });

        Assert.False(result.IsTerminal);
        Assert.Equal("ActionReferencePending", result.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancelAsyncDoesNotCancelTheOriginalActionWhileReplayIsUnresolved(bool hasReplayRequest)
    {
        var cancellations = Substitute.For<IMuleCancellationService>();
        var replay = Substitute.For<IMuleReplayService>();
        var requestId = MuleUlid.NewUlid();
        replay.GetReplayAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<MuleReplayInfo>((MuleReplayInfo)null!));
        var dispatch = CreateDispatch();
        dispatch.Metadata["Mule.ActionId"] = JsonValue.Create(MuleUlid.NewUlid().ToString());
        dispatch.Metadata["Recovery.DispatchCancellationSourceStatus"] = JsonValue.Create("ReplayPending");
        if (hasReplayRequest)
        {
            dispatch.Metadata["Recovery.DispatchReplayRequestId"] = JsonValue.Create(requestId.ToString());
        }

        var result = await new MuleTaskDispatchCancellationService(cancellations, replay, Substitute.For<IMuleStorage>())
            .CancelAsync(new TaskDispatchCancellationRequest { Dispatch = dispatch, Reason = "terminal" });

        Assert.False(result.IsTerminal);
        await cancellations.DidNotReceiveWithAnyArgs().CancelAsync(default, default);
    }

    private static TaskDispatch CreateDispatch()
        => new()
        {
            Id = Id.New(),
            TaskExecutionAttemptId = Id.New(),
            DispatchType = "Messaging",
            DispatchStatus = "Cancelled"
        };
}
