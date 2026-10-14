using KnOwl.Contracts.Artifacts;

namespace Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;

internal sealed class KnOwlCommandContractArtifactsDocument
{
    public string CommandKey { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public KnOwlContractArtifactDocument RequestArtifact { get; set; }

    public KnOwlContractArtifactDocument ReplyArtifact { get; set; }

    public CommandContractArtifacts<ContractArtifact> ToCommandContractArtifacts()
        => new(
            CommandKey ?? string.Empty,
            Version ?? string.Empty,
            RequestArtifact?.ToContractArtifact(),
            ReplyArtifact?.ToContractArtifact());
}
