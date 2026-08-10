namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public sealed class RuntimeEnvironmentModel
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }
    public string Description { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed record UpsertRuntimeEnvironmentInput(
    string EnvironmentId,
    string Name,
    string Code,
    string Description);

