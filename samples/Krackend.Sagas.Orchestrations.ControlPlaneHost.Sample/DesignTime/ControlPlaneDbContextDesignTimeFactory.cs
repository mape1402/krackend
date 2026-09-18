using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.DesignTime;

/// <summary>
/// Creates the control-plane storage context for Entity Framework design-time operations.
/// </summary>
public sealed class ControlPlaneDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ControlPlaneDbContext>
{
    /// <inheritdoc />
    public ControlPlaneDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString();
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseSqlServer(
                connectionString,
                sql => sql.MigrationsAssembly(typeof(ControlPlaneDbContextDesignTimeFactory).Assembly.GetName().Name))
            .Options;

        var sqlServerStorageModelCustomizer = new ControlPlaneSqlServerStorageModelCustomizer();
        var storageOptions = Options.Create(new ControlPlaneEntityFrameworkStorageOptions
        {
            ConfigureModel = sqlServerStorageModelCustomizer.Configure
        });

        return new ControlPlaneDbContext(options, storageOptions);
    }

    private static string ResolveConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__ControlPlaneDocker")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? ReadConnectionString("ControlPlaneDocker")
            ?? ReadConnectionString("Default");

        return string.IsNullOrWhiteSpace(configured)
            ? throw new InvalidOperationException("Set ConnectionStrings:ControlPlaneDocker or ConnectionStrings:Default for the control-plane host.")
            : configured;
    }

    private static string ReadConnectionString(string name)
    {
        foreach (var basePath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var filePath = Path.Combine(basePath, "appsettings.json");
            if (!File.Exists(filePath))
            {
                continue;
            }

            using var stream = File.OpenRead(filePath);
            using var document = System.Text.Json.JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings) &&
                connectionStrings.TryGetProperty(name, out var value))
            {
                return value.GetString();
            }
        }

        return null;
    }
}
