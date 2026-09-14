using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles update orchestration definition command requests.
/// </summary>
public sealed class UpdateOrchestrationDefinitionCommandHandler : IRequestHandler<UpdateOrchestrationDefinitionCommand, bool>
{
    private readonly IOrchestrationDefinitionRepository _repository;
    private readonly IDomainRepository _domainRepository;
    private readonly ITeamRepository _teamRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOrchestrationDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public UpdateOrchestrationDefinitionCommandHandler(
        IOrchestrationDefinitionRepository repository,
        IDomainRepository domainRepository,
        ITeamRepository teamRepository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _domainRepository = domainRepository ?? throw new ArgumentNullException(nameof(domainRepository));
        _teamRepository = teamRepository ?? throw new ArgumentNullException(nameof(teamRepository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(UpdateOrchestrationDefinitionCommand request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.OrchestrationDefinition current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);
        var domain = await _domainRepository.GetById(PrimitiveParser.ParseId(request.DomainId), cancellationToken);
        var ownerTeam = await _teamRepository.GetById(PrimitiveParser.ParseId(request.OwnerTeamId), cancellationToken);
        if (!domain.IsActive)
        {
            throw new InvalidOperationException("Selected domain is disabled.");
        }
        if (ownerTeam is null || !ownerTeam.IsActive)
        {
            throw new InvalidOperationException("Selected team is invalid or disabled.");
        }

        current.Name = request.Name;
        current.DomainId = domain.Id;
        current.DomainDisplayName = domain.DisplayName;
        current.Domain = domain.DisplayName;
        current.OwnerTeamId = ownerTeam.Id;
        current.OwnerTeamDisplayName = ownerTeam.DisplayName;
        current.OwnerTeam = ownerTeam.DisplayName;
        current.Description = request.Description;
        current.Tags = request.Tags?.Distinct().ToList() ?? new List<string>();
        current.UpdatedOnUtc = DateTime.UtcNow;
        current.UpdatedBy = request.UpdatedBy;

        await _repository.Update(current, cancellationToken);
        return true;
    }
}


