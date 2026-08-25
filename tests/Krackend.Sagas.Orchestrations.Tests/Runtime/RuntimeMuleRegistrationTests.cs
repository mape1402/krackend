using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mule;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeMuleRegistrationTests
{
    [Fact]
    public void RuntimeMuleRegistrationConfiguresArtifactLifecycleLanes()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.Configure<Krackend.Sagas.Orchestrations.Runtime.Replication.RuntimeReplicaOptions>(options =>
        {
            options.ReplicaId = "replica-a";
        });

        services
            .AddKrackendOrchestrationsRuntime()
            .AddMule(_ => { });

        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<MuleSettings>>().Value;

        Assert.Equal(MuleRecoveryMode.Polling, settings.RecoveryMode);
        Assert.True(settings.Lanes.ContainsKey(RuntimeArtifactProjectionSchedulerDefaults.Lane));
        Assert.True(settings.Lanes.ContainsKey("runtime-standup:replica-a"));
    }
}
