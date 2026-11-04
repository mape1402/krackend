namespace Krackend.Sagas.Orchestrations.Tests.Design;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using NSubstitute;

public sealed class DesignerAtomicCommandTests
{
    [Fact]
    public async Task TransformationCommandUsesAtomicRepositoryOperationWhenRevisionIsProvided()
    {
        var repository = Substitute.For<ITaskRepository>();
        var id = Id.New();
        var transformation = new TransformationDefinition { Engine = EngineType.DSL };
        var command = new SetTaskTransformationCommand(id.ToString(), transformation) { ExpectedDesignRevision = "revision" };
        Assert.True(await new SetTaskTransformationCommandHandler(repository).Handle(command, CancellationToken.None));
        await repository.Received(1).SetTransformationIfUnchanged(id, transformation, "revision", CancellationToken.None);
        await repository.DidNotReceiveWithAnyArgs().SetTransformation(default, default!);
    }
}
