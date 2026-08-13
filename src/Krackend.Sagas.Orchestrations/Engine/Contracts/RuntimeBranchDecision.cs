namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Represents the result of evaluating runtime branch rules.
/// </summary>
internal sealed record RuntimeBranchDecision(
    bool HasRules,
    bool IsTaken,
    bool IsSupported,
    string RuleId,
    string TargetStageKey,
    int TargetStageIndex,
    string Reason)
{
    public static RuntimeBranchDecision NoRules { get; } = new(false, false, true, string.Empty, string.Empty, -1, "No branch rules configured.");

    public static RuntimeBranchDecision NotTaken(string reason)
        => new(true, false, true, string.Empty, string.Empty, -1, reason);

    public static RuntimeBranchDecision Unsupported(string ruleId, string reason)
        => new(true, false, false, ruleId, string.Empty, -1, reason);

    public static RuntimeBranchDecision Taken(string ruleId, string targetStageKey, int targetStageIndex)
        => new(true, true, true, ruleId, targetStageKey, targetStageIndex, "Branch rule matched.");
}
