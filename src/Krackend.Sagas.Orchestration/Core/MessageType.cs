namespace Krackend.Sagas.Orchestration.Core
{
    /// <summary>
    /// Represents the type of message used in the saga orchestration process.
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// Indicates a message that represents an event.
        /// </summary>
        Event,

        /// <summary>
        /// Indicates a message that represents a command.
        /// </summary>
        Command,
        
        /// <summary>
        /// Indicates a message that represents a compensation command, typically used to undo previous actions.
        /// </summary>
        CompensationCommand
    }
}
