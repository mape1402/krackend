namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

internal static class RuntimeReactiveHubGroups
{
    public static string Environment(string environmentKey) => $"runtime:environment:{environmentKey}";

    public static string Instance(string instanceId) => $"runtime:instance:{instanceId}";
}
