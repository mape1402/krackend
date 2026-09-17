namespace Krackend.Security.Api;

/// <summary>
/// Request used to change a subject enabled state.
/// </summary>
public sealed class SetKrackendSubjectEnabledRequest
{
    /// <summary>
    /// Gets or sets a value indicating whether the subject is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
}
