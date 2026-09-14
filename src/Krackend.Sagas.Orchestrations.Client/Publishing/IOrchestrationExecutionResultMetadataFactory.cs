namespace Krackend.Sagas.Orchestrations.Client.Publishing;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

internal interface IOrchestrationExecutionResultMetadataFactory
{
    OrchestrationExecutionResultMetadata CreateSuccess(
        Type requestType,
        Type responseType,
        OrchestrationOperationOptions options);

    OrchestrationExecutionResultMetadata CreateFailure(
        Type requestType,
        Exception exception,
        OrchestrationOperationOptions options);
}
