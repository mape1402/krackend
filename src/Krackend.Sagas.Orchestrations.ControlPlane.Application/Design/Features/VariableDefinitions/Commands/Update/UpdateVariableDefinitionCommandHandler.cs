using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles update variable definition command requests.
/// </summary>
public sealed class UpdateVariableDefinitionCommandHandler : IRequestHandler<UpdateVariableDefinitionCommand, bool>
{
    private readonly IVariableDefinitionRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateVariableDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public UpdateVariableDefinitionCommandHandler(IVariableDefinitionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(UpdateVariableDefinitionCommand request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.VariableDefinition current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);

        current.Key = request.Key;
        current.DisplayName = request.DisplayName;
        current.Description = request.Description;
        current.Scope = request.Scope;
        current.ValueType = request.ValueType;
        current.DefaultValue = request.DefaultValue;
        current.IsRequired = request.IsRequired;
        current.IsSensitive = request.IsSensitive;

        await _repository.Update(current, cancellationToken);
        return true;
    }
}

