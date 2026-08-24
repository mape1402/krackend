using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles create parallel group definition command requests.
/// </summary>
public sealed class CreateParallelGroupDefinitionCommandHandler : IRequestHandler<CreateParallelGroupDefinitionCommand, string>
{
    private readonly IParallelGroupRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateParallelGroupDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public CreateParallelGroupDefinitionCommandHandler(IParallelGroupRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Identifier or textual result of the operation.</returns>
    public async Task<string> Handle(CreateParallelGroupDefinitionCommand request, CancellationToken cancellationToken)
    {
        Id id = string.IsNullOrWhiteSpace(request.Id)
            ? Id.New()
            : PrimitiveParser.ParseId(request.Id);

        ParallelGroupDefinition model = new()
        {
            Id = id,
            StageDefinitionId = PrimitiveParser.ParseId(request.StageDefinitionId),
            Name = request.Name ?? string.Empty,
            JoinPolicy = request.JoinPolicy,
            MaxParallelAgents = request.MaxParallelAgents,
        };

        await _repository.Create(model, cancellationToken);
        return id.ToString();
    }
}

