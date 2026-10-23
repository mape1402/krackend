using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Reviews;

/// <summary>Lists lifecycle reviews independently from orchestration roadmaps.</summary>
public sealed class IndexModel : PageModel
{
    private const int PageSize = 25;
    private readonly IOrchestrationVersionReviewApplicationService _reviewService;
    private readonly IOrchestrationVersionApplicationService _versionService;
    private readonly IOrchestrationApplicationService _orchestrationService;

    /// <summary>Initializes a new instance.</summary>
    public IndexModel(
        IOrchestrationVersionReviewApplicationService reviewService,
        IOrchestrationVersionApplicationService versionService,
        IOrchestrationApplicationService orchestrationService)
    {
        _reviewService = reviewService ?? throw new ArgumentNullException(nameof(reviewService));
        _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
        _orchestrationService = orchestrationService ?? throw new ArgumentNullException(nameof(orchestrationService));
    }

    /// <summary>Gets review rows for the active page.</summary>
    public IReadOnlyCollection<ReviewRow> Rows { get; private set; } = Array.Empty<ReviewRow>();

    /// <summary>Gets the active page number.</summary>
    public int PageNumber { get; private set; } = 1;

    /// <summary>Gets the total available pages.</summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>Gets the user-facing load failure.</summary>
    public string ErrorMessage { get; private set; } = string.Empty;

    /// <summary>Loads the paged review queue.</summary>
    public async Task OnGetAsync(int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _reviewService.GetAll(
                new GetAllOrchestrationVersionReviewsQuery(new ApplicationPagedSettings
                {
                    PageNumber = Math.Max(1, pageNumber),
                    PageSize = PageSize,
                }),
                cancellationToken);

            var rows = new List<ReviewRow>();
            foreach (var review in result.Rows)
            {
                OrchestrationVersionModel version;
                try
                {
                    version = await _versionService.GetById(
                        new GetOrchestrationVersionByIdQuery(review.OrchestrationVersionId),
                        cancellationToken);
                }
                catch (KeyNotFoundException)
                {
                    rows.Add(new ReviewRow(review, null, null));
                    continue;
                }

                try
                {
                    var orchestration = await _orchestrationService.GetById(
                        new GetOrchestrationDefinitionByIdQuery(version.OrchestrationDefinitionId),
                        cancellationToken);
                    rows.Add(new ReviewRow(review, version, orchestration));
                }
                catch (KeyNotFoundException)
                {
                    rows.Add(new ReviewRow(review, version, null));
                }
            }

            Rows = rows;
            PageNumber = result.PageNumber;
            TotalPages = Math.Max(1, result.TotalPages);
        }
        catch (Exception)
        {
            ErrorMessage = "Lifecycle reviews could not be loaded. Try again in a moment.";
        }
    }

    /// <summary>Represents a review enriched with its roadmap version when it still exists.</summary>
    public sealed record ReviewRow(
        OrchestrationVersionReviewModel Review,
        OrchestrationVersionModel Version,
        OrchestrationDefinitionModel Orchestration);
}
