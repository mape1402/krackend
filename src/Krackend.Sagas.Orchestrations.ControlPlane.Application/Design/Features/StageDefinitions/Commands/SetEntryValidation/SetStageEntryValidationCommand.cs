using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents a request to update the validation executed before a stage starts.
/// </summary>
public sealed record SetStageEntryValidationCommand(
    string Id,
    ValidationDefinition EntryValidation) : IRequest<bool>;
