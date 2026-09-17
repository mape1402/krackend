namespace Krackend.Security.Storage;

/// <summary>
/// Represents a paged query result.
/// </summary>
/// <typeparam name="T">Row type.</typeparam>
public sealed class KrackendPagedResult<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KrackendPagedResult{T}"/> class.
    /// </summary>
    /// <param name="pageNumber">Current page number.</param>
    /// <param name="totalPages">Total page count.</param>
    /// <param name="totalRows">Total row count.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="rows">Page rows.</param>
    public KrackendPagedResult(int pageNumber, int totalPages, long totalRows, int pageSize, IEnumerable<T> rows)
    {
        PageNumber = pageNumber;
        TotalPages = totalPages;
        TotalRows = totalRows;
        PageSize = pageSize;
        Rows = rows?.ToArray() ?? [];
    }

    /// <summary>
    /// Gets the current page number.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Gets the total page count.
    /// </summary>
    public int TotalPages { get; }

    /// <summary>
    /// Gets the total row count.
    /// </summary>
    public long TotalRows { get; }

    /// <summary>
    /// Gets the page size.
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// Gets the page rows.
    /// </summary>
    public IReadOnlyCollection<T> Rows { get; }
}
