namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using System.Text.Json.Serialization;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable polymorphic task-configuration contract.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$artifactType")]
[JsonDerivedType(typeof(MessagingTaskConfigurationArtifact), "messaging")]
[JsonDerivedType(typeof(HttpTaskConfigurationArtifact), "http")]
[JsonDerivedType(typeof(PluginTaskConfigurationArtifact), "plugin")]
[JsonDerivedType(typeof(HumanApprovalTaskConfigurationArtifact), "humanApproval")]
public interface ITaskConfigurationArtifact
{
    /// <summary>
    /// Gets task kind associated with this configuration.
    /// </summary>
    TaskKind Kind { get; }
}
