namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Portable, draft-only envelope used to move an orchestration design between control-plane instances.
/// </summary>
public sealed class OrchestrationVersionPackage
{
    /// <summary>Current portable package schema.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Stable package discriminator.</summary>
    public const string Kind = "krackend-orchestration-draft";

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string PackageKind { get; init; } = Kind;

    public DateTimeOffset ExportedOnUtc { get; init; } = DateTimeOffset.UtcNow;

    public required OrchestrationVersionPackageSource Source { get; init; }

    /// <summary>Canonical artifact JSON carrying the complete version graph and schema snapshots.</summary>
    public required string ArtifactJson { get; init; }

    /// <summary>SHA-256 of <see cref="ArtifactJson"/>.</summary>
    public required string ArtifactChecksum { get; init; }

    /// <summary>Design-only values not represented in deployable artifacts.</summary>
    public IReadOnlyDictionary<string, string> TriggerKeys { get; init; } = new Dictionary<string, string>();

    /// <summary>Design-only parallel group names indexed by their artifact identifier.</summary>
    public IReadOnlyDictionary<string, string> ParallelGroupNames { get; init; } = new Dictionary<string, string>();
}

/// <summary>Human-readable source information retained for auditability only.</summary>
public sealed record OrchestrationVersionPackageSource(
    string OrchestrationKey,
    string VersionId,
    string Status,
    string Label,
    string OrchestrationName = "",
    string OrchestrationDescription = "",
    string DomainName = "",
    string OwnerTeamName = "");

/// <summary>Safe inspection result returned before an import is applied.</summary>
public sealed record OrchestrationVersionPackageInspection(
    bool IsValid,
    string Message,
    string OrchestrationKey = "",
    int TriggerCount = 0,
    int StageCount = 0,
    int TaskCount = 0,
    string OrchestrationName = "",
    string OrchestrationDescription = "",
    string DomainName = "");

/// <summary>Local ownership details required when importing a draft into a new orchestration.</summary>
public sealed record CreateImportedOrchestrationInput(
    string Name,
    string DomainId,
    string OwnerTeamId,
    string Description,
    IReadOnlyCollection<string>? Tags = null);

/// <summary>Identifiers created by a portable draft import.</summary>
public sealed record ImportedOrchestrationDraft(string OrchestrationDefinitionId, string OrchestrationVersionId);
