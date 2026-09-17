using Microsoft.AspNetCore.Http;

namespace Krackend.Sagas.Orchestrations.Runtime.Api;

internal static class ApiEndpointResults
{
    public static IResult BadRequest(string message) => Results.BadRequest(new { error = message });

    public static IResult NotFound(string message) => Results.NotFound(new { error = message });
}
