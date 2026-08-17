using System.Diagnostics;

namespace Krackend.Sagas.Orchestrations.Engine;

internal sealed class RuntimeProfile
{
    private static readonly bool Enabled = string.Equals(
        Environment.GetEnvironmentVariable("KRACKEND_RUNTIME_PROFILE"),
        "1",
        StringComparison.OrdinalIgnoreCase);

    private static readonly RuntimeProfile Null = new();

    private readonly string _name;
    private readonly Stopwatch _stopwatch;
    private long _lastElapsed;

    private RuntimeProfile(string name)
    {
        _name = name;
        _stopwatch = Stopwatch.StartNew();
    }

    private RuntimeProfile()
    {
        _name = string.Empty;
        _stopwatch = new Stopwatch();
    }

    public static RuntimeProfile Start(string name)
        => Enabled ? new RuntimeProfile(name) : Null;

    public void Mark(string step)
    {
        if (!Enabled)
            return;

        var elapsed = _stopwatch.ElapsedMilliseconds;
        Console.WriteLine($"KRACKEND_PROFILE {_name} {step} deltaMs={elapsed - _lastElapsed} totalMs={elapsed}");
        _lastElapsed = elapsed;
    }

    public void Stop()
    {
        if (!Enabled)
            return;

        _stopwatch.Stop();
        Console.WriteLine($"KRACKEND_PROFILE {_name} completed totalMs={_stopwatch.ElapsedMilliseconds}");
    }
}
