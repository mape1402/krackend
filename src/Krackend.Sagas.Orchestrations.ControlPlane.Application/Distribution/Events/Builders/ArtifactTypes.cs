namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

internal static class ArtifactTypes
{
    public const string NewOrchestrationVersion = "orchestration-version.new";
    public const string DeprecatedOrchestrationVersion = "orchestration-version.deprecated";
    public const string ArchivedOrchestrationVersion = "orchestration-version.archived";
    [Obsolete("Legacy artifact type retained to read previously generated artifacts.")]
    public const string OrchestrationVersionSnapshot = "orchestration-version-snapshot";
    public const string SchemaVersion = "1.0";
}
