using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Web;

public static class RuntimeEngineEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapKrackendSagasOrchestrationsEngineEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/runtime/triggers", async (
            RuntimeTriggerRequest request,
            IRuntimeTriggerInteractionService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.Enqueue(request, cancellationToken);
            return result.Accepted ? Results.Ok(result) : Results.BadRequest(result);
        });

        endpoints.MapPost("/runtime/engine/process-all", async (
            int? maxItems,
            IRuntimePendingWorkProcessor processor,
            CancellationToken cancellationToken) =>
        {
            var result = await processor.ProcessDueWork(DateTime.UtcNow, cancellationToken);
            return Results.Ok(result);
        });

        return endpoints;
    }
}
