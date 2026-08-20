using DataScorpio.Profiles;
using Payments.Business;
using Microsoft.EntityFrameworkCore;
using TurtlePath.EntityFrameworkCore;
using TurtlePath.Testing;
using TurtlePath.Testing.EntityFrameworkCore;
using TurtlePath.Testing.Integration;

namespace Payments.Tests.Testing;

public static class TemplateTestHost
{
    private static readonly Action<QueryProfileRegistryBuilder> DefaultDataScorpioProfiles =
        profiles => profiles.FromAssembly(typeof(Constants).Assembly);

    public static TurtlePathTestHostBuilder CreateUnitHost()
        => TurtlePathTestHost.Create();

    public static TurtlePathTestHostBuilder CreateIntegrationHost<TDbContext>(
        Action<QueryProfileRegistryBuilder> configureDataScorpio = null)
        where TDbContext : DbContext, IDbContext
    {
        var businessAssembly = typeof(Constants).Assembly;

        return TurtlePathTestHost
            .Create()
            .UsePelicanTesting(businessAssembly)
            .UseOctoMapTesting(businessAssembly)
            .UseCrabalidatorTesting(businessAssembly)
            .UseSpiderTesting(businessAssembly)
            .UseDataScorpioTesting(configureDataScorpio ?? DefaultDataScorpioProfiles)
            .UseSqliteDbContext<TDbContext>(options => options with
            {
                ConfigurationAssemblies = [typeof(TDbContext).Assembly]
            });
    }
}
