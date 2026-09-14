namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Bootstrap;

internal interface IDesignHostSeedDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
