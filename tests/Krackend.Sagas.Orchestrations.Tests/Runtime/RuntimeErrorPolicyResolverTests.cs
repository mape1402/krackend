using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeErrorPolicyResolverTests
{
    [Theory]
    [InlineData(OnErrorPolicy.Continue, RuntimeErrorPolicyAction.Continue)]
    [InlineData(OnErrorPolicy.Stop, RuntimeErrorPolicyAction.Stop)]
    [InlineData(OnErrorPolicy.StopAndCompensate, RuntimeErrorPolicyAction.StartCompensation)]
    public void Resolve_MapsDesignPolicyToRuntimeAction(OnErrorPolicy policy, RuntimeErrorPolicyAction expectedAction)
    {
        var resolver = new RuntimeErrorPolicyResolver();

        var decision = resolver.Resolve(policy);

        Assert.Equal(policy, decision.Policy);
        Assert.Equal(expectedAction, decision.Action);
    }
}
