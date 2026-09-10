namespace Krackend.Sagas.Orchestrations.SchemaRegistry;

/// <summary>
/// Defines the orchestration payload role represented by a schema contract.
/// </summary>
public enum SchemaContractKind
{
    /// <summary>
    /// The schema kind has not been specified.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// A trigger event payload contract.
    /// </summary>
    Event = 1,

    /// <summary>
    /// A task command request payload contract.
    /// </summary>
    CommandRequest = 2,

    /// <summary>
    /// A task command response payload contract.
    /// </summary>
    CommandResponse = 3,

    /// <summary>
    /// An orchestration variable payload contract.
    /// </summary>
    Variable = 4,

    /// <summary>
    /// A metadata payload contract.
    /// </summary>
    Metadata = 5
}
