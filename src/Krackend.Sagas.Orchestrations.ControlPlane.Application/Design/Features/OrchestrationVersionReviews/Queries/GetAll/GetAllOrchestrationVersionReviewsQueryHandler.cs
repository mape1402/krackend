namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

/// <summary>Handles paged lifecycle-review listings independent of a roadmap.</summary>
public sealed class GetAllOrchestrationVersionReviewsQueryHandler
    : IRequestHandler<GetAllOrchestrationVersionReviewsQuery, ApplicationPagedResult<OrchestrationVersionReviewModel>>
{
    private readonly IOrchestrationVersionReviewRepository _repository;

    /// <summary>Initializes a new instance.</summary>
    public GetAllOrchestrationVersionReviewsQueryHandler(IOrchestrationVersionReviewRepository repository)
        => _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    /// <inheritdoc />
    public async Task<ApplicationPagedResult<OrchestrationVersionReviewModel>> Handle(
        GetAllOrchestrationVersionReviewsQuery request,
        CancellationToken cancellationToken)
    {
        var reviews = await _repository.GetAll(
            PrimitiveParser.ToStoragePagedSettings(request.Settings),
            cancellationToken);

        return new ApplicationPagedResult<OrchestrationVersionReviewModel>(
            reviews.PageNumber,
            reviews.TotalPages,
            reviews.TotalRows,
            reviews.PageSize,
            reviews.Rows.Select(GetOrchestrationVersionReviewByIdQueryHandler.Map).ToArray());
    }
}
