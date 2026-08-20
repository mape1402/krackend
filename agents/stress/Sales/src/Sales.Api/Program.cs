using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseDefaultServiceProvider(opts =>
    {
        opts.ValidateOnBuild = false; // Change to false to disable validation of services at build time
    });

    builder.Configuration
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .AddEnvironmentVariables()
        .AddUserSecrets<Program>(optional: true);

    builder.Host.UseSerilog();

    Log.Logger = new LoggerConfiguration()
                   .ReadFrom.Configuration(builder.Configuration)
                   .CreateLogger();

    builder.Services.AddDefaults(builder.Configuration, builder.Environment);

    var app = builder.Build();

    app.UseDefaults(app.Environment);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly.");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}
