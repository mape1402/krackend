namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using NSubstitute;

public sealed class OrchestrationPolicyCatalogTests
{
    [Theory]
    [InlineData("key", "key")]
    [InlineData("status", "isActive")]
    [InlineData("name", "name")]
    [InlineData("unexpected", "name")]
    public async Task SearchPassesPagingAndSortingToStorage(string sort, string expectedField)
    {
        var repository = Substitute.For<IOrchestrationDefinitionRepository>();
        var definition = new OrchestrationDefinition { Id = Id.New(), Key = "sales", Name = "Sales", CreatedBy = "test", IsActive = true };
        repository.Search(Arg.Any<PagedSettings>(), "sales", Arg.Any<CancellationToken>())
            .Returns(new PagedResult<OrchestrationDefinition>(2, 3, 5, 2, [definition]));
        var service = new OrchestrationNodePolicyApplicationService(Substitute.For<IOrchestrationNodePolicyRepository>(), repository);

        var result = await service.SearchOrchestrations(new ApplicationPagedSettings { PageNumber = 2, PageSize = 2 }, "sales", sort, true);

        Assert.Equal(5, result.TotalRows);
        Assert.Equal("Sales", Assert.Single(result.Rows).Name);
        await repository.Received(1).Search(Arg.Is<PagedSettings>(x => x.PageNumber == 2 && x.PageSize == 2 && x.Sorts.First().Field == expectedField && x.Sorts.First().Descending), "sales", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrchestrationReturnsSelectedDefinition()
    {
        var repository = Substitute.For<IOrchestrationDefinitionRepository>();
        var definition = new OrchestrationDefinition { Id = Id.New(), Key = "sales", Name = "Sales", CreatedBy = "test", IsActive = true };
        repository.GetById(definition.Id, Arg.Any<CancellationToken>()).Returns(definition);
        var service = new OrchestrationNodePolicyApplicationService(Substitute.For<IOrchestrationNodePolicyRepository>(), repository);

        var result = await service.GetOrchestration(definition.Id.ToString());

        Assert.Equal(definition.Id.ToString(), result.Id);
        Assert.True(result.IsActive);
    }
}
