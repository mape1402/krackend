using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles create orchestration definition command requests.
/// </summary>
public sealed class CreateOrchestrationDefinitionCommandHandler : IRequestHandler<CreateOrchestrationDefinitionCommand, string>
{
    private readonly IOrchestrationDefinitionRepository _repository;
    private readonly IDomainRepository _domainRepository;
    private readonly ITeamRepository _teamRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateOrchestrationDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public CreateOrchestrationDefinitionCommandHandler(
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
    /// <returns>Identifier or textual result of the operation.</returns>
    public async Task<string> Handle(CreateOrchestrationDefinitionCommand request, CancellationToken cancellationToken)
    {
        Id id = Id.New();
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

        OrchestrationDefinition model = new()
        {
            Id = id,
            Key = request.Key,
            Name = request.Name,
            Description = request.Description,
            Domain = domain.DisplayName,
            DomainId = domain.Id,
            DomainDisplayName = domain.DisplayName,
            OwnerTeam = ownerTeam.DisplayName,
            OwnerTeamId = ownerTeam.Id,
            OwnerTeamDisplayName = ownerTeam.DisplayName,
            Tags = request.Tags?.Distinct().ToList() ?? new List<string>(),
            IsActive = true,
            CreatedOnUtc = DateTime.UtcNow,
            CreatedBy = request.CreatedBy,
            UpdatedOnUtc = default,
            UpdatedBy = string.Empty,
        };

        await _repository.Create(model, cancellationToken);
        return id.ToString();
    }
}


