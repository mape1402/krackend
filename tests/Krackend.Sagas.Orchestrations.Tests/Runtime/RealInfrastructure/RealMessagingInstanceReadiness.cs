namespace Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

internal sealed record RealMessagingInstanceReadiness(
    OrchestrationInstance? MatchingInstance,
    int TotalInstances,
    string ObservedInstances,
    string Diagnostics)
{
    public override string ToString()
        => $"MatchingInstance = {MatchingInstance}, TotalInstances = {TotalInstances}, ObservedInstances = {ObservedInstances}, Diagnostics = {Diagnostics}";
}
