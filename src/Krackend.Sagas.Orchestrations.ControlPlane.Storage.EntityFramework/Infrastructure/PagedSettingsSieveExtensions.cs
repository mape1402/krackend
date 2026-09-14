using Sieve.Models;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;

/// <summary>
/// Represents PagedSettingsSieveExtensions.
/// </summary>
internal static class PagedSettingsSieveExtensions
{
    /// <summary>
    /// Executes ToSieveModel.
    /// </summary>
    public static SieveModel ToSieveModel(this PagedSettings pagedSettings)
    {
        var pageNumber = Math.Max(pagedSettings?.PageNumber ?? 1, 1);
        var pageSize = Math.Max(pagedSettings?.PageSize ?? 25, 1);

        return new SieveModel
        {
            Page = pageNumber,
            PageSize = pageSize,
            Sorts = BuildSorts(pagedSettings?.Sorts),
            Filters = BuildFilters(pagedSettings?.Filters),
        };
    }

    /// <summary>
    /// Executes BuildSorts.
    /// </summary>
    private static string BuildSorts(IEnumerable<QuerySort> sorts)
    {
        if (sorts is null || !sorts.Any())
        {
            return null;
        }

        return string.Join(",", sorts
            .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Field))
            .Select(x => x.Descending ? $"-{x.Field}" : x.Field));
    }

    /// <summary>
    /// Executes BuildFilters.
    /// </summary>
    private static string BuildFilters(IEnumerable<QueryFilter> filters)
    {
        if (filters is null || !filters.Any())
        {
            return null;
        }

        var expressions = new List<string>();

        foreach (var filter in filters)
        {
            if (filter is null || string.IsNullOrWhiteSpace(filter.Field))
            {
                continue;
            }

            var op = ResolveOperator(filter.Operator);
            var value = EscapeFilterValue(filter.Value);
            expressions.Add($"{filter.Field}{op}{value}");
        }

        return expressions.Count == 0 ? null : string.Join(",", expressions);
    }

    /// <summary>
    /// Executes ResolveOperator.
    /// </summary>
    private static string ResolveOperator(string @operator)
    {
        var op = (@operator ?? "eq").Trim().ToLowerInvariant();
        return op switch
        {
            "eq" => "==",
            "neq" => "!=",
            "contains" => "@=",
            "startswith" => "_=",
            "endswith" => "_-=",
            "gt" => ">",
            "gte" => ">=",
            "lt" => "<",
            "lte" => "<=",
            _ => "==",
        };
    }

    /// <summary>
    /// Executes EscapeFilterValue.
    /// </summary>
    private static string EscapeFilterValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }
}

