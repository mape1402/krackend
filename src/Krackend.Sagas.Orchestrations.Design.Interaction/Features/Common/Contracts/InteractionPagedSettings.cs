namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents interaction paged settings.
/// </summary>
public sealed class InteractionPagedSettings
{
    /// <summary>
    /// Gets or sets the page number.
    /// </summary>
    public int PageNumber { get; set; } = 1;
    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; } = 25;
    /// <summary>
    /// Gets or sets the filters.
    /// </summary>
    public IEnumerable<InteractionFilter> Filters { get; set; } = Array.Empty<InteractionFilter>();
    /// <summary>
    /// Gets or sets the sorts.
    /// </summary>
    public IEnumerable<InteractionSort> Sorts { get; set; } = Array.Empty<InteractionSort>();
}


