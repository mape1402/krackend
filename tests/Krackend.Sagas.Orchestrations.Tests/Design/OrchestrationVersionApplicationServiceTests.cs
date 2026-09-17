using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using NSubstitute;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Tests.Design;

public sealed class OrchestrationVersionApplicationServiceTests
{
    [Fact]
    public async Task MethodsDelegateCommandsAndQueriesToMediator()
    {
        var mediator = Substitute.For<IMediator>();
        var service = new OrchestrationVersionApplicationService(mediator);
        var version = new OrchestrationVersionModel { Id = "version-1", Version = "1.0.0" };
        var page = new ApplicationPagedResult<OrchestrationVersionModel>(1, 1, 1, 10, [version]);

        mediator.Send(Arg.Any<CreateOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
            .Returns("created-version");
        mediator.Send(Arg.Any<UpdateOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        mediator.Send(Arg.Any<SetOrchestrationVersionInReviewCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        mediator.Send(Arg.Any<ReturnOrchestrationVersionToDraftCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        mediator.Send(Arg.Any<ReopenOrchestrationVersionReviewCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        mediator.Send(Arg.Any<ApproveOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        mediator.Send(Arg.Any<DeployOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        mediator.Send(Arg.Any<DeprecateOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        mediator.Send(Arg.Any<ArchiveOrchestrationVersionCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        mediator.Send(Arg.Any<GetOrchestrationVersionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(version);
        mediator.Send(Arg.Any<GetOrchestrationVersionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(page);

        var create = new CreateOrchestrationVersionCommand(
            Id.New().ToString(),
            "1.0.0",
            OrchestrationVersionStatus.Draft,
            "1.0.0",
            "Initial version",
            "checksum",
            "notes",
            "tester");
        var update = new UpdateOrchestrationVersionCommand("version-1", "1.0.1", "Updated", "checksum-2", "notes", "tester");
        var inReview = new SetOrchestrationVersionInReviewCommand("version-1");
        var draft = new ReturnOrchestrationVersionToDraftCommand("version-1");
        var reopen = new ReopenOrchestrationVersionReviewCommand("version-1", "tester");
        var approve = new ApproveOrchestrationVersionCommand("version-1", "approver");
        var deploy = new DeployOrchestrationVersionCommand("version-1", "deployer");
        var deprecate = new DeprecateOrchestrationVersionCommand("version-1", "deployer");
        var archive = new ArchiveOrchestrationVersionCommand("version-1", "archiver");
        var getById = new GetOrchestrationVersionByIdQuery("version-1");
        var getAll = new GetOrchestrationVersionsQuery("orch-1", new ApplicationPagedSettings { PageNumber = 1, PageSize = 10 });

        Assert.Equal("created-version", await service.Create(create));
        Assert.True(await service.Update(update));
        Assert.True(await service.SetInReview(inReview));
        Assert.True(await service.ReturnToDraft(draft));
        Assert.True(await service.ReopenReview(reopen));
        Assert.True(await service.Approve(approve));
        Assert.True(await service.Deploy(deploy));
        Assert.True(await service.Deprecate(deprecate));
        Assert.True(await service.Archive(archive));
        Assert.Same(version, await service.GetById(getById));
        Assert.Same(page, await service.GetAll(getAll));

        await mediator.Received(1).Send(create, Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(update, Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(inReview, Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(draft, Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(reopen, Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(approve, Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(deploy, Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(deprecate, Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(archive, Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(getById, Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(getAll, Arg.Any<CancellationToken>());
    }
}
