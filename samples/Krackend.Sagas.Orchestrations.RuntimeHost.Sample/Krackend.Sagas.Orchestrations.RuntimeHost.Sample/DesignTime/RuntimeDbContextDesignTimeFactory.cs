using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.DesignTime;

/// <summary>
/// Creates the runtime storage context for Entity Framework design-time operations.
/// </summary>
public sealed class RuntimeDbContextDesignTimeFactory : IDesignTimeDbContextFactory<RuntimeDbContext>
{
    /// <inheritdoc />
    public RuntimeDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString();
        var options = new DbContextOptionsBuilder<RuntimeDbContext>()
            .UseSqlServer(
                connectionString,
                sql =>
                {
                    sql.MigrationsAssembly(typeof(RuntimeDbContextDesignTimeFactory).Assembly.GetName().Name);
                    sql.MigrationsHistoryTable("__RuntimeStorageMigrationsHistory", "Runtime");
                })
            .Options;

        var sqlServerStorageModelCustomizer = new RuntimeSqlServerStorageModelCustomizer();
        var storageOptions = Options.Create(new RuntimeEntityFrameworkStorageOptions
        {
            ConfigureModel = sqlServerStorageModelCustomizer.Configure
        });

        return new RuntimeDbContext(options, storageOptions);
    }

    private static string ResolveConnectionString()
    {
        string? configured = Environment.GetEnvironmentVariable("ConnectionStrings__Mule")
            ?? ReadConnectionString("Mule");

        return string.IsNullOrWhiteSpace(configured)
            ? throw new InvalidOperationException("Set ConnectionStrings:Mule for the runtime host.")
            : configured;
    }

    private static string? ReadConnectionString(string name)
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
