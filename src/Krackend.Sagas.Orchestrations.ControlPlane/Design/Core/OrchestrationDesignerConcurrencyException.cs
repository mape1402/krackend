namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>Indicates that a designer document changed before it could be saved.</summary>
public sealed class OrchestrationDesignerConcurrencyException : InvalidOperationException
{
    /// <summary>Initializes a designer concurrency conflict.</summary>
    public OrchestrationDesignerConcurrencyException() : base("The designer document changed before it could be saved.") { }

    /// <summary>Initializes a designer concurrency conflict with its underlying cause.</summary>
    public OrchestrationDesignerConcurrencyException(Exception innerException)
        : base("The designer document changed before it could be saved.", innerException) { }
}
