namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

internal static class RuntimeReactiveHubGroups
{
    public const string Runtime = "runtime:node";

    public static string Instance(string instanceId) => $"runtime:instance:{instanceId}";
}
