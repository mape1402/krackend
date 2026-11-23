using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Tests.Runtime;
using Krackend.Sagas.Orchestrations.Tests.WebUI;

// Run the same source tests in process when VSTest cannot create its socket.
// Runs the original tests, including real SQLite/Mule compositions where selected.
var results = new List<object>();
var failed = 0;
var requested = args.Where(argument => argument.EndsWith("Tests", StringComparison.Ordinal)).ToArray();
var selected = new HashSet<string>(requested.Length == 0 ? new[] {
    "RuntimeConcurrencyRegressionTests", "RuntimeReactiveEventMessageTests", "RedisRuntimeReactiveFanoutTests",
    "RemoteCommandDispatchActionTests", "RemoteCommandDispatcherTests", "IntakeAndMuleActionForwardingTests",
    "MuleBufferingTests", "RuntimeMuleRegistrationTests", "MessagingDecisionE2ETests"
} : requested, StringComparer.Ordinal);
foreach (var type in typeof(RuntimeConcurrencyRegressionTests).Assembly.GetTypes().Where(type => selected.Contains(type.Name)))
foreach (var method in type.GetMethods().Where(method => method.GetCustomAttribute<FactAttribute>() is not null))
foreach (var arguments in method.GetCustomAttributes<InlineDataAttribute>().SelectMany(data => data.GetData(method))
             .DefaultIfEmpty([]))
{
    var watch = Stopwatch.StartNew();
    Console.WriteLine($"RUN {type.Name}.{method.Name}");
    try
    {
        var result = method.Invoke(Activator.CreateInstance(type), arguments);
        if (result is Task task) await task.WaitAsync(TimeSpan.FromMinutes(12));
        results.Add(new { test = $"{type.Name}.{method.Name}({string.Join(',', arguments)})", passed = true, elapsedMs = watch.ElapsedMilliseconds });
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAILED {type.Name}.{method.Name}: {(exception is TargetInvocationException ? exception.InnerException : exception)}");
        results.Add(new { test = $"{type.Name}.{method.Name}({string.Join(',', arguments)})", passed = false, elapsedMs = watch.ElapsedMilliseconds,
            error = (exception is TargetInvocationException ? exception.InnerException : exception)?.ToString() });
    }
}
Directory.CreateDirectory("artifacts");
if (results.Count == 0) throw new InvalidOperationException("No regression cases were selected.");
var reportPath = Environment.GetEnvironmentVariable("KRACKEND_REGRESSION_REPORT") ?? "artifacts/concurrency-regression-results.json";
await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(new { total = results.Count, failed, passed = results.Count - failed,
    report = Path.GetFullPath(reportPath) }));
return failed == 0 ? 0 : 1;
