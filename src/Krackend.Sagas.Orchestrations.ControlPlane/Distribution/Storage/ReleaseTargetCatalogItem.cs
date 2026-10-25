using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

/// <summary>A release target and the runtime name used in catalog views.</summary>
public sealed record ReleaseTargetCatalogItem(ReleaseTarget Target, string RuntimeNodeName);
