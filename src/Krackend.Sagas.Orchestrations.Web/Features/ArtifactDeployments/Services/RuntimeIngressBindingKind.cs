namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Identifies the logical runtime ingress binding kind.
/// </summary>
public enum RuntimeIngressBindingKind
{
    /// <summary>
    /// Binding starts an orchestration instance.
    /// </summary>
    Trigger = 1,

    /// <summary>
    /// Binding resumes an orchestration task response.
    /// </summary>
    BackChannel = 2
}
