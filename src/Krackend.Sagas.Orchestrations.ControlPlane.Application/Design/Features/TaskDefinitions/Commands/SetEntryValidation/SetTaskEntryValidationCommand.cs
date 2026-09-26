using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents a request to update the validation executed before a task dispatch.
/// </summary>
public sealed record SetTaskEntryValidationCommand(
    string Id,
    ValidationDefinition EntryValidation) : IRequest<bool>;
