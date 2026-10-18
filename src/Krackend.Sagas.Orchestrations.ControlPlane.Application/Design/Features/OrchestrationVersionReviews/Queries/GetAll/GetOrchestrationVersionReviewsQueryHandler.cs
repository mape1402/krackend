namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

/// <summary>Handles requests for an orchestration version's lifecycle review history.</summary>
public sealed class GetOrchestrationVersionReviewsQueryHandler
    : IRequestHandler<GetOrchestrationVersionReviewsQuery, IReadOnlyCollection<OrchestrationVersionReviewModel>>
{
    private readonly IOrchestrationVersionReviewRepository _repository;

    /// <summary>Initializes a new instance.</summary>
    public GetOrchestrationVersionReviewsQueryHandler(IOrchestrationVersionReviewRepository repository)
        => _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<OrchestrationVersionReviewModel>> Handle(
        GetOrchestrationVersionReviewsQuery request,
        CancellationToken cancellationToken)
    {
        var reviews = await _repository.GetAllForVersion(
            PrimitiveParser.ParseId(request.OrchestrationVersionId),
            cancellationToken);

        return reviews
            .Select(GetOrchestrationVersionReviewByIdQueryHandler.Map)
            .ToArray();
    }
}
