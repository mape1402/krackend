using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Artifacts;

public sealed class IndexModel : PageModel
{
    private const string NewVersionArtifactType = "orchestration-version.new";
    private const string DeprecatedArtifactType = "orchestration-version.deprecated";
    private const string ArchivedArtifactType = "orchestration-version.archived";
    private const string LegacySnapshotArtifactType = "orchestration-version-snapshot";
    private const int PageSize = 300;
    private const int MaxPageReads = 50;

    private readonly IArtifactApplicationService _service;
    private readonly IOrchestrationNodePolicyApplicationService _policyService;

    public IndexModel(
        IArtifactApplicationService service,
        IOrchestrationNodePolicyApplicationService policyService)
    {
        _service = service;
        _policyService = policyService;
    }

    public IReadOnlyCollection<OrchestrationPolicyDefinitionModel> Orchestrations { get; private set; } = Array.Empty<OrchestrationPolicyDefinitionModel>();
    public IReadOnlyCollection<ArtifactModel> Rows { get; private set; } = Array.Empty<ArtifactModel>();
    public IReadOnlyDictionary<string, ArtifactOrchestrationSummary> ArtifactSummaryByOrchestrationId { get; private set; } =
        new Dictionary<string, ArtifactOrchestrationSummary>(StringComparer.Ordinal);
    [BindProperty(SupportsGet = true)] public string OrchestrationId { get; set; } = string.Empty;
    public OrchestrationPolicyDefinitionModel SelectedOrchestration { get; private set; } = null!;

    public ArtifactOrchestrationSummary GetSummary(string orchestrationId)
        => !string.IsNullOrWhiteSpace(orchestrationId) &&
           ArtifactSummaryByOrchestrationId.TryGetValue(orchestrationId, out var summary)
            ? summary
            : ArtifactOrchestrationSummary.Empty;

    /// <summary>Returns a concise, color-distinct presentation for an artifact lifecycle type.</summary>
    public ArtifactTypePresentation GetArtifactTypePresentation(string artifactType)
        => artifactType switch
        {
            NewVersionArtifactType => new ArtifactTypePresentation("New version", "od-artifact-type-new"),
            DeprecatedArtifactType => new ArtifactTypePresentation("Deprecated", "od-artifact-type-deprecated"),
            ArchivedArtifactType => new ArtifactTypePresentation("Archived", "od-artifact-type-archived"),
            LegacySnapshotArtifactType => new ArtifactTypePresentation("Snapshot", "od-artifact-type-legacy"),
            _ => new ArtifactTypePresentation(string.IsNullOrWhiteSpace(artifactType) ? "Unknown" : artifactType, "od-artifact-type-legacy"),
        };

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        Orchestrations = await _policyService.GetOrchestrations(cancellationToken);
        var rows = await LoadArtifacts(cancellationToken);
        ArtifactSummaryByOrchestrationId = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.OrchestrationDefinitionId))
            .GroupBy(x => x.OrchestrationDefinitionId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => new ArtifactOrchestrationSummary(g.Count(), g.Max(x => x.CreatedAtUtc)),
                StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(OrchestrationId))
        {
            Rows = Array.Empty<ArtifactModel>();
            return;
        }

        SelectedOrchestration = Orchestrations.FirstOrDefault(x => string.Equals(x.Id, OrchestrationId, StringComparison.Ordinal));
        Rows = rows
            .Where(x => string.Equals(x.OrchestrationDefinitionId, OrchestrationId, StringComparison.Ordinal))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToArray();
    }

    private async Task<ArtifactModel[]> LoadArtifacts(CancellationToken cancellationToken)
    {
        var rows = new List<ArtifactModel>();
        var pageNumber = 1;
        var totalPages = 1;

        do
        {
            var result = await _service.GetAll(
                new ApplicationPagedSettings { PageNumber = pageNumber, PageSize = PageSize },
                cancellationToken);
            rows.AddRange(result.Rows);
            totalPages = Math.Max(result.TotalPages, 1);
            pageNumber++;
        }
        while (pageNumber <= totalPages && pageNumber <= MaxPageReads);

        return rows.ToArray();
    }

    public sealed record ArtifactOrchestrationSummary(int ArtifactCount, DateTime? LatestCreatedAtUtc)
    {
        public static ArtifactOrchestrationSummary Empty { get; } = new(0, null);
    }

    /// <summary>Describes the visible label and theme-safe class for an artifact type.</summary>
    public sealed record ArtifactTypePresentation(string Label, string CssClass);
}
