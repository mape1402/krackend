namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents an invalid DSL configuration detected before artifact publication.
/// </summary>
public sealed class OrchestrationArtifactDslValidationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationArtifactDslValidationException"/> class.
    /// </summary>
    /// <param name="path">Logical artifact path where the error was found.</param>
    /// <param name="message">Validation error message.</param>
    /// <param name="diagnosticsJson">Serialized diagnostics produced by the DSL engine.</param>
    public OrchestrationArtifactDslValidationException(
        string path,
        string message,
        string diagnosticsJson = "{}")
        : base(message)
    {
        Path = path;
        DiagnosticsJson = string.IsNullOrWhiteSpace(diagnosticsJson) ? "{}" : diagnosticsJson;
    }

    /// <summary>
    /// Gets the logical artifact path where the error was found.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets serialized diagnostics produced by the DSL engine.
    /// </summary>
    public string DiagnosticsJson { get; }
}
