using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles create variable definition command requests.
/// </summary>
public sealed class CreateVariableDefinitionCommandHandler : IRequestHandler<CreateVariableDefinitionCommand, string>
{
    private readonly IVariableDefinitionRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateVariableDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public CreateVariableDefinitionCommandHandler(IVariableDefinitionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Identifier or textual result of the operation.</returns>
    public async Task<string> Handle(CreateVariableDefinitionCommand request, CancellationToken cancellationToken)
    {
        Id id = Id.New();

        VariableDefinition model = new()
        {
            Id = id,
            OrchestrationVersionId = PrimitiveParser.ParseId(request.OrchestrationVersionId),
            Key = request.Key,
            DisplayName = request.DisplayName,
            Description = request.Description,
            Scope = request.Scope,
            ValueType = request.ValueType,
            DefaultValue = request.DefaultValue,
            IsRequired = request.IsRequired,
            IsSensitive = request.IsSensitive,
        };

        await _repository.Create(model, cancellationToken);
        return id.ToString();
    }
}

