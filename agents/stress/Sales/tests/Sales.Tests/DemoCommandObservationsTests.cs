using Krackend.Demo;

namespace Sales.Tests;

public sealed class DemoCommandObservationsTests
{
    [Fact]
    public void RecordAndReadRemainBoundedAndSafeUnderConcurrentUpdates()
    {
        var observations = new DemoCommandObservations(enabled: true);

        Parallel.For(0, 50_000, index =>
        {
            observations.Record("load", $"operation-{index}", "completed");
            if (index % 31 == 0)
                _ = observations.Read("load");
        });

        var retained = observations.Read("load");
        Assert.NotEmpty(retained);
        Assert.InRange(retained.Length, 1, 4096);
    }
}
