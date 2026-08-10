using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Interaction;

namespace Krackend.Sagas.Orchestrations.Tests.Design;

public sealed class OrchestrationVersionTransitionPolicyTests
{
    [Theory]
    [InlineData(OrchestrationVersionStatus.Draft, OrchestrationVersionStatus.InReview)]
    [InlineData(OrchestrationVersionStatus.InReview, OrchestrationVersionStatus.Draft)]
    [InlineData(OrchestrationVersionStatus.InReview, OrchestrationVersionStatus.Approved)]
    [InlineData(OrchestrationVersionStatus.Approved, OrchestrationVersionStatus.InReview)]
    [InlineData(OrchestrationVersionStatus.Approved, OrchestrationVersionStatus.Deployed)]
    [InlineData(OrchestrationVersionStatus.Deployed, OrchestrationVersionStatus.Deprecated)]
    [InlineData(OrchestrationVersionStatus.Deprecated, OrchestrationVersionStatus.Archived)]
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
    [InlineData(OrchestrationVersionStatus.Deployed, OrchestrationVersionStatus.Approved)]
    [InlineData(OrchestrationVersionStatus.Archived, OrchestrationVersionStatus.Draft)]
    public void EnsureCanTransitionRejectsInvalidLifecycleMoves(
        OrchestrationVersionStatus from,
        OrchestrationVersionStatus to)
    {
        var policy = new OrchestrationVersionTransitionPolicy();

        Assert.False(policy.CanTransition(from, to));
        Assert.Throws<InvalidOperationException>(() => policy.EnsureCanTransition(from, to));
    }
}
