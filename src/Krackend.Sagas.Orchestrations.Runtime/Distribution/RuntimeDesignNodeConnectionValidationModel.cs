namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Represents a Runtime design node connection validation result.
/// </summary>
public sealed class RuntimeDesignNodeConnectionValidationModel
{
    /// <summary>
    /// Gets or sets a value indicating whether validation succeeded.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the validation message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
