using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Text.RegularExpressions;

namespace Krackend.Demo.RuntimeHost;

/// <summary>
/// Translates the portable runtime's filtered-index predicates to PostgreSQL.
/// The functional model remains identical; only provider SQL quoting and boolean
/// literals differ from SQL Server.
/// </summary>
internal sealed partial class RuntimePostgresStorageModelCustomizer
{
    public void Configure(ModelBuilder modelBuilder)
    {
        // StatisticsRevision advances on every engine transition, but the only
        // lookup it serves is the one-time legacy backfill at revision zero.
        // PostgreSQL cannot use HOT when an updated column is referenced even by
        // a partial-index predicate, so remove this index for the PostgreSQL host.
        var instances = modelBuilder.Entity<OrchestrationInstance>();
        var revisionIndex = instances.HasIndex(instance => instance.StatisticsRevision).Metadata;
        instances.Metadata.RemoveIndex(revisionIndex);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var index in entityType.GetIndexes())
            {
                var filter = index.GetFilter();
                if (string.IsNullOrWhiteSpace(filter)) continue;

                filter = SqlServerIdentifier().Replace(filter, match => $"\"{match.Groups[1].Value}\"");
                foreach (var property in index.DeclaringEntityType.GetProperties()
                    .Where(property => property.ClrType == typeof(bool) || property.ClrType == typeof(bool?)))
                {
                    var column = $"\"{property.GetColumnName()}\"";
                    filter = Regex.Replace(filter, $"{Regex.Escape(column)}\\s*=\\s*1\\b", $"{column} = TRUE");
                    filter = Regex.Replace(filter, $"{Regex.Escape(column)}\\s*=\\s*0\\b", $"{column} = FALSE");
                }
                index.SetFilter(filter);
            }
        }
    }

    [GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex SqlServerIdentifier();
}
