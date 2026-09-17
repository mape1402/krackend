namespace Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RuntimeChaosRealInfrastructureCollection : ICollectionFixture<RuntimeRealInfrastructureFixture>
{
    public const string Name = "Runtime chaos real infrastructure";
}
