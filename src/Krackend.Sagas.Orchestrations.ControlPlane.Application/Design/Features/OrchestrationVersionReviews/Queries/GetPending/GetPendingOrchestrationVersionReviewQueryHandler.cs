namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

/// <summary>Handles a request for the currently pending review of a version.</summary>
public sealed class GetPendingOrchestrationVersionReviewQueryHandler
    : IRequestHandler<GetPendingOrchestrationVersionReviewQuery, OrchestrationVersionReviewModel>
{
    private readonly IOrchestrationVersionReviewRepository _repository;

    public GetPendingOrchestrationVersionReviewQueryHandler(IOrchestrationVersionReviewRepository repository)
        => _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<OrchestrationVersionReviewModel> Handle(
        GetPendingOrchestrationVersionReviewQuery request,
        CancellationToken cancellationToken)
    {
        var review = await _repository.GetPendingForVersion(
            PrimitiveParser.ParseId(request.OrchestrationVersionId),
            cancellationToken);

        return review is null
            ? null
            : GetOrchestrationVersionReviewByIdQueryHandler.Map(review);
    }
}
