namespace Krackend.Sagas.Orchestrations.RuntimeHost.Mongo.Sample.Bootstrap;

public interface IHappyPathOrchestrationSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
