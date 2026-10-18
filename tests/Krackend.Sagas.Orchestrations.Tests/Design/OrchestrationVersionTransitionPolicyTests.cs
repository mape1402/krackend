using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

namespace Krackend.Sagas.Orchestrations.Tests.Design;

public sealed class OrchestrationVersionTransitionPolicyTests
{
    [Theory]
    [InlineData(OrchestrationVersionStatus.Draft, OrchestrationVersionStatus.PendingDeployment)]
    [InlineData(OrchestrationVersionStatus.Draft, OrchestrationVersionStatus.Abandoned)]
    [InlineData(OrchestrationVersionStatus.PendingDeployment, OrchestrationVersionStatus.Draft)]
    [InlineData(OrchestrationVersionStatus.PendingDeployment, OrchestrationVersionStatus.Deployed)]
    [InlineData(OrchestrationVersionStatus.Deployed, OrchestrationVersionStatus.PendingDeprecation)]
    [InlineData(OrchestrationVersionStatus.PendingDeprecation, OrchestrationVersionStatus.Deployed)]
    [InlineData(OrchestrationVersionStatus.PendingDeprecation, OrchestrationVersionStatus.Deprecated)]
    [InlineData(OrchestrationVersionStatus.Deprecated, OrchestrationVersionStatus.PendingArchive)]
    [InlineData(OrchestrationVersionStatus.PendingArchive, OrchestrationVersionStatus.Deprecated)]
    [InlineData(OrchestrationVersionStatus.PendingArchive, OrchestrationVersionStatus.Archived)]
    public void CanTransitionAllowsOnlySupportedLifecycleMoves(
        OrchestrationVersionStatus from,
        OrchestrationVersionStatus to)
    {
        var policy = new OrchestrationVersionTransitionPolicy();

        Assert.True(policy.CanTransition(from, to));
        policy.EnsureCanTransition(from, to);
    }

    [Theory]
    [InlineData(OrchestrationVersionStatus.Draft, OrchestrationVersionStatus.Deployed)]
    [InlineData(OrchestrationVersionStatus.Deployed, OrchestrationVersionStatus.Deprecated)]
    [InlineData(OrchestrationVersionStatus.Deprecated, OrchestrationVersionStatus.Archived)]
    [InlineData(OrchestrationVersionStatus.PendingDeployment, OrchestrationVersionStatus.Abandoned)]
    [InlineData(OrchestrationVersionStatus.Archived, OrchestrationVersionStatus.Draft)]
    public void EnsureCanTransitionRejectsInvalidLifecycleMoves(
        OrchestrationVersionStatus from,
        OrchestrationVersionStatus to)
    {
        var policy = new OrchestrationVersionTransitionPolicy();

        Assert.False(policy.CanTransition(from, to));
        Assert.Throws<InvalidOperationException>(() => policy.EnsureCanTransition(from, to));
    }

    [Fact]
    public void UnknownLifecycleStatesHaveNoAllowedTransitions()
    {
        var policy = new OrchestrationVersionTransitionPolicy();
        var unknown = (OrchestrationVersionStatus)999;

        Assert.False(policy.CanTransition(unknown, OrchestrationVersionStatus.Draft));
        Assert.Empty(policy.GetAllowedTargets(unknown));
        Assert.Throws<InvalidOperationException>(() => policy.EnsureCanTransition(unknown, OrchestrationVersionStatus.Draft));
    }
}
