using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Artifacts;

/// <summary>
/// Displays runtime artifacts and allows operators to request ingress standup again.
/// </summary>
public sealed class IndexModel : PageModel
{
    private readonly IRuntimeArtifactRepository _artifactRepository;
    private readonly IRuntimeArtifactReadyNotifier _artifactReadyNotifier;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    public IndexModel(
        IRuntimeArtifactRepository artifactRepository,
        IRuntimeArtifactReadyNotifier artifactReadyNotifier)
    {
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
        _artifactReadyNotifier = artifactReadyNotifier ?? throw new ArgumentNullException(nameof(artifactReadyNotifier));
    }

    /// <summary>
    /// Gets artifact rows.
    /// </summary>
    public IReadOnlyCollection<ArtifactRowViewModel> Artifacts { get; private set; } = Array.Empty<ArtifactRowViewModel>();

    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string Search { get; set; } = string.Empty;

    /// <summary>
    /// Handles the initial page request.
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        await LoadAsync(cancellationToken);
    }

    /// <summary>
    /// Requests local and peer replicas to stand up the selected ready artifact.
    /// </summary>
    public async Task<IActionResult> OnPostRequestStandupAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseId(artifactId, out var parsedArtifactId))
        {
            SetMessage("Invalid artifact", "Artifact id is invalid.", "error");
            return RedirectToPage();
        }

        try
        {
            var artifact = await _artifactRepository.GetById(parsedArtifactId, cancellationToken);
            if (artifact.Status != RuntimeOrchestrationArtifactStatus.Ready || !artifact.IsActive)
            {
                SetMessage("Artifact is not ready", "Only active ready artifacts can request ingress standup.", "warning");
                return RedirectToPage();
            }

            await _artifactReadyNotifier.NotifyReadyAsync(
                RuntimeArtifactReadyGossipMessage.FromArtifact(artifact),
                cancellationToken);
            SetMessage("Standup requested", $"Ingress standup was requested for {artifact.OrchestrationDefinitionKey} v{artifact.Version}.", "success");
        }
        catch (Exception exception)
        {
            SetMessage("Standup request failed", exception.Message, "error");
        }

        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var artifacts = await _artifactRepository.GetAll(cancellationToken);
        Artifacts = artifacts
            .Where(MatchesSearch)
            .OrderByDescending(x => x.DeployedOnUtc)
            .Select(ToRow)
            .ToArray();
    }

    private bool MatchesSearch(RuntimeOrchestrationArtifact artifact)
    {
        if (string.IsNullOrWhiteSpace(Search))
        {
            return true;
        }

        var search = Search.Trim();
        return Contains(artifact.Id.ToString(), search) ||
            Contains(artifact.OrchestrationDefinitionKey, search) ||
            Contains(artifact.Version.ToString(), search) ||
            Contains(artifact.ArtifactChecksum.Value, search);
    }

    private static ArtifactRowViewModel ToRow(RuntimeOrchestrationArtifact artifact)
        => new()
        {
            ArtifactId = artifact.Id.ToString(),
            OrchestrationDefinitionKey = artifact.OrchestrationDefinitionKey,
            Version = artifact.Version.ToString(),
            Status = artifact.Status.ToString(),
            IsActive = artifact.IsActive,
            IngressGeneration = artifact.IngressGeneration,
            Checksum = artifact.ArtifactChecksum.Value,
            DeployedOnUtc = artifact.DeployedOnUtc,
            ActivatedOnUtc = artifact.ActivatedOnUtc,
            ProjectionStartedOnUtc = artifact.ProjectionStartedOnUtc,
            ProjectionCompletedOnUtc = artifact.ProjectionCompletedOnUtc,
            ProjectionFailedOnUtc = artifact.ProjectionFailedOnUtc,
            ProjectionError = artifact.ProjectionError ?? string.Empty
        };

    private static bool Contains(string value, string search)
        => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;

    private void SetMessage(string title, string body, string type)
    {
        TempData["OrchestratorMessage.Title"] = title;
        TempData["OrchestratorMessage.Body"] = body;
        TempData["OrchestratorMessage.Type"] = type;
    }

    private static bool TryParseId(string value, out Id id)
    {
        id = default;
        if (!Ulid.TryParse(value, out var parsed))
        {
            return false;
        }

        id = new Id(parsed);
        return true;
    }
}
