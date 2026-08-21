namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Bootstrap;

public interface IHappyPathOrchestrationSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
