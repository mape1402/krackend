using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>Reads bounded pages for the runtime artifact catalog.</summary>
public interface IRuntimeArtifactUiCatalog
{
    /// <summary>Lists orchestration groups without loading their payloads or histories.</summary>
    Task<RuntimeArtifactGroupPage> GetGroups(int page, int pageSize, string search, CancellationToken cancellationToken = default);

    /// <summary>Lists versions for a single orchestration.</summary>
    Task<RuntimeArtifactVersionPage> GetVersions(string orchestrationKey, int page, int pageSize, string search, CancellationToken cancellationToken = default);
}

/// <summary>Summary of deployed versions of an orchestration.</summary>
public sealed record RuntimeArtifactGroup(string Key, int VersionCount, int ActiveCount, DateTime LatestDeployedOnUtc);

/// <summary>One page of orchestration groups.</summary>
public sealed record RuntimeArtifactGroupPage(IReadOnlyCollection<RuntimeArtifactGroup> Items, long TotalCount, int PageNumber, int PageSize);

/// <summary>One page of orchestration versions.</summary>
public sealed record RuntimeArtifactVersionPage(IReadOnlyCollection<RuntimeArtifactCatalogItem> Items, long TotalCount, int PageNumber, int PageSize);

/// <summary>Runtime version details excluding the artifact payload.</summary>
public sealed record RuntimeArtifactCatalogItem(
    Id Id, string OrchestrationKey, string Version, string Status, string LifecycleStatus,
    string LastReleaseId, string LastControlPlaneArtifactId, string ReleaseStrategy, bool IsActive,
    long IngressGeneration, string Checksum, DateTime DeployedOnUtc, DateTime? ActivatedOnUtc,
    DateTime? ProjectionStartedOnUtc, DateTime? ProjectionCompletedOnUtc, DateTime? ProjectionFailedOnUtc, string ProjectionError);

/// <summary>Reads the bounded history of one runtime artifact.</summary>
public interface IRuntimeArtifactHistoryCatalog
{
    /// <summary>Returns a chronological page of lifecycle records for one artifact.</summary>
    Task<RuntimeArtifactHistoryPage> GetPage(Id artifactId, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>One page of lifecycle records.</summary>
public sealed record RuntimeArtifactHistoryPage(IReadOnlyCollection<RuntimeArtifactDeploymentRecord> Items, long TotalCount, int PageNumber, int PageSize);
