namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Mongo.Sample.Bootstrap;

internal interface IDesignHostSeedDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
