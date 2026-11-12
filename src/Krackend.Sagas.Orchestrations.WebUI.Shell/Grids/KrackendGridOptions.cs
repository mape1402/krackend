namespace Krackend.Sagas.Orchestrations.WebUI.Shell.Grids;

/// <summary>Configures one paged catalog grid.</summary>
public sealed class KrackendGridOptions
{
    public string Id { get; set; }
    public string AriaLabel { get; set; }
    public KrackendGridBinding Binding { get; set; }
    public int PageSize { get; set; } = 25;
    public IReadOnlyList<KrackendGridColumn> Columns { get; set; } = [];
}

/// <summary>Identifies the server endpoint used by a catalog grid.</summary>
public sealed class KrackendGridBinding
{
    public string Source { get; set; }
}

/// <summary>Describes one catalog column.</summary>
public sealed record KrackendGridColumn(string Field, string Title, bool Sortable = true);
