namespace Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RuntimeRealInfrastructureCollection : ICollectionFixture<RuntimeRealInfrastructureFixture>
{
    public const string Name = "Runtime real infrastructure";
}
