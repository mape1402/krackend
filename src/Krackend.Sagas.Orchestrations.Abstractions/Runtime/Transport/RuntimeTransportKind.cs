namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;

/// <summary>
/// Identifies the transport used to receive or dispatch orchestration work.
/// </summary>
public enum RuntimeTransportKind
{
    /// <summary>
    /// Transport was not specified.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Message broker transport.
    /// </summary>
    Message = 1,

    /// <summary>
    /// HTTP transport.
    /// </summary>
    Http = 2,

    /// <summary>
    /// gRPC transport.
    /// </summary>
    Grpc = 3
}
