using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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

        endpoints.MapPost("/runtime/engine/process-next", async (
            IRuntimeTriggerInteractionService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ProcessNext(cancellationToken);
            return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
        });

        endpoints.MapPost("/runtime/engine/process-all", async (
            int? maxItems,
            IRuntimeTriggerInteractionService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ProcessAll(maxItems ?? 25, cancellationToken);
            return Results.Ok(result);
        });

        return endpoints;
    }
}
