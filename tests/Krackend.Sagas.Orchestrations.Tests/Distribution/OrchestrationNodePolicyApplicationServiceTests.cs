namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using NSubstitute;

public sealed class OrchestrationNodePolicyApplicationServiceTests
{
    [Fact]
    public async Task GetOrchestrationsMapsPagedDefinitionsForPolicySelection()
    {
        var repository = Substitute.For<IOrchestrationNodePolicyRepository>();
        var orchestrationRepository = Substitute.For<IOrchestrationDefinitionRepository>();
        var activeId = Id.New();
        var inactiveId = Id.New();
        orchestrationRepository.GetAll(Arg.Any<PagedSettings>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var settings = call.ArgAt<PagedSettings>(0);
                Assert.Equal(1, settings.PageNumber);
                Assert.Equal(int.MaxValue, settings.PageSize);
                Assert.Empty(settings.Filters);
                Assert.Empty(settings.Sorts);
                return new PagedResult<OrchestrationDefinition>(
                    1,
                    1,
                    2,
                    int.MaxValue,
                    [
                        Definition(activeId, "sales.sale.created", "Sale Created", true),
                        Definition(inactiveId, "sales.sale.cancelled", "Sale Cancelled", false)
                    ]);
            });
        var service = new OrchestrationNodePolicyApplicationService(repository, orchestrationRepository);

        var result = await service.GetOrchestrations();

        Assert.Collection(
            result,
            item =>
            {
                Assert.Equal(activeId.ToString(), item.Id);
                Assert.Equal("sales.sale.created", item.Key);
                Assert.Equal("Sale Created", item.Name);
                Assert.True(item.IsActive);
            },
            item =>
            {
                Assert.Equal(inactiveId.ToString(), item.Id);
                Assert.False(item.IsActive);
            });
    }

    [Fact]
    public async Task GetAndGetByOrchestrationIdsMapAllowedRuntimeNodes()
    {
        var repository = Substitute.For<IOrchestrationNodePolicyRepository>();
        var orchestrationRepository = Substitute.For<IOrchestrationDefinitionRepository>();
        var orchestrationId = Id.New().ToString();
        var runtimeNodeId = Id.New();
        var secondNodeId = Id.New();
        repository.GetAllowedRuntimeNodeIds(orchestrationId, Arg.Any<CancellationToken>())
            .Returns([runtimeNodeId, secondNodeId]);
        repository.GetAllowedRuntimeNodeIdsByOrchestrationIds(
                Arg.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { orchestrationId })),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, IReadOnlyCollection<Id>>
            {
                [orchestrationId] = [runtimeNodeId, secondNodeId]
            });
        var service = new OrchestrationNodePolicyApplicationService(repository, orchestrationRepository);

        var single = await service.Get(orchestrationId);
        var many = await service.GetByOrchestrationIds([orchestrationId]);

        Assert.Equal(orchestrationId, single.OrchestrationDefinitionId);
        Assert.Equal([runtimeNodeId.ToString(), secondNodeId.ToString()], single.RuntimeNodeIds);
        Assert.True(many.TryGetValue(orchestrationId, out var nodeIds));
        Assert.Equal([runtimeNodeId.ToString(), secondNodeId.ToString()], nodeIds);
    }

    [Fact]
    public async Task ReplaceValidatesDefinitionAndPersistsDistinctRuntimeNodes()
    {
        var repository = Substitute.For<IOrchestrationNodePolicyRepository>();
        var orchestrationRepository = Substitute.For<IOrchestrationDefinitionRepository>();
        var orchestrationId = Id.New();
        var firstNodeId = Id.New();
        var secondNodeId = Id.New();
        orchestrationRepository.GetById(orchestrationId, Arg.Any<CancellationToken>())
            .Returns(Definition(orchestrationId, "sales.sale.created", "Sale Created", true));
        IReadOnlyCollection<OrchestrationAllowedRuntimeNode>? captured = null;
        repository.Replace(
                orchestrationId.ToString(),
                Arg.Do<IReadOnlyCollection<OrchestrationAllowedRuntimeNode>>(nodes => captured = nodes),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var service = new OrchestrationNodePolicyApplicationService(repository, orchestrationRepository);

        await service.Replace(new ReplaceOrchestrationNodePolicyInput(
            orchestrationId.ToString(),
            [firstNodeId.ToString(), "", firstNodeId.ToString(), secondNodeId.ToString()],
            "  designer  "));

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.Count);
        Assert.All(captured, node =>
        {
            Assert.Equal(orchestrationId.ToString(), node.OrchestrationDefinitionId);
            Assert.Equal("designer", node.CreatedBy);
            Assert.True(node.CreatedAtUtc <= DateTime.UtcNow);
        });
        Assert.Equal([firstNodeId, secondNodeId], captured.Select(node => node.RuntimeNodeId).ToArray());
    }

    [Fact]
    public async Task ReplaceRejectsMissingOrUnknownOrchestrationDefinition()
    {
        var repository = Substitute.For<IOrchestrationNodePolicyRepository>();
        var orchestrationRepository = Substitute.For<IOrchestrationDefinitionRepository>();
        var orchestrationId = Id.New();
        orchestrationRepository.GetById(orchestrationId, Arg.Any<CancellationToken>())
            .Returns<Task<OrchestrationDefinition>>(_ => throw new KeyNotFoundException());
        var service = new OrchestrationNodePolicyApplicationService(repository, orchestrationRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Replace(new ReplaceOrchestrationNodePolicyInput("", [], "")));
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Replace(new ReplaceOrchestrationNodePolicyInput(
            orchestrationId.ToString(),
            [Id.New().ToString()],
            "")));
        Assert.Equal("Orchestration definition was not found.", missing.Message);
        await repository.DidNotReceiveWithAnyArgs().Replace(default!, default!, default);
    }

    private static OrchestrationDefinition Definition(Id id, string key, string name, bool active)
        => new()
        {
            Id = id,
            Key = key,
            Name = name,
            CreatedBy = "tests",
            CreatedOnUtc = DateTime.UtcNow,
            IsActive = active
        };
}
