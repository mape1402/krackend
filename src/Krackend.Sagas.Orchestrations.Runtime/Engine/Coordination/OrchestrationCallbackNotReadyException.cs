namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;

/// <summary>Indicates a valid callback whose dispatch has not reached its response state yet.</summary>
internal sealed class OrchestrationCallbackNotReadyException(string message) : InvalidOperationException(message);
