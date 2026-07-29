namespace Krackend.EventSourcing.Pelican.Sample.State;

public sealed record CustomerState(
    string CustomerId,
    string Name,
    string Email,
    decimal Balance,
    string LastRenameChange,
    string LastBalanceChange,
    bool IsCreated)
{
    public static CustomerState Empty { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        0m,
        string.Empty,
        string.Empty,
        false);
}
