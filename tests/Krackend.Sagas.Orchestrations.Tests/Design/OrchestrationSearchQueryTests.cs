using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Design;

public sealed class OrchestrationSearchQueryTests
{
    [Fact]
    public async Task SearchPassesTrimmedTextToStorage()
    {
        var repository = Substitute.For<IOrchestrationDefinitionRepository>();
        var mapper = Substitute.For<IOrchestrationDefinitionApplicationMapper>();
        var result = new PagedResult<OrchestrationDefinition>(1, 0, 0, 25, []);
        repository.Search(Arg.Any<PagedSettings>(), "sale", Arg.Any<CancellationToken>()).Returns(result);
        var handler = new GetOrchestrationDefinitionsQueryHandler(repository, mapper);

        await handler.Handle(new GetOrchestrationDefinitionsQuery(new ApplicationPagedSettings(), "  sale  "), CancellationToken.None);

        await repository.Received(1).Search(Arg.Any<PagedSettings>(), "sale", Arg.Any<CancellationToken>());
        await repository.DidNotReceive().GetAll(Arg.Any<PagedSettings>(), Arg.Any<CancellationToken>());
        mapper.Received(1).ToPagedModel(result);
    }
}
