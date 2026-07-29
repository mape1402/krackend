using Krackend.EventSourcing.Contracts;

namespace Krackend.EventSourcing.Sqlite.Sample.State;

[StateSchema("CustomerState")]
public sealed record CustomerState(
    string CustomerId,
    string Name,
    string Email,
    bool IsCreated)
{
    public static CustomerState Empty { get; } = new(string.Empty, string.Empty, string.Empty, false);
}
