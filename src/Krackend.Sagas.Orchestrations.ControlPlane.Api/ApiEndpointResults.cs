using Microsoft.AspNetCore.Http;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Api;

internal static class ApiEndpointResults
{
    public static IResult Created(string id) => Results.Created(string.Empty, new { id });

    public static IResult Ok(bool succeeded) => Results.Ok(new { succeeded });

    public static IResult BadRequest(string message) => Results.BadRequest(new { error = message });

    public static IResult NotFound(string message) => Results.NotFound(new { error = message });

    public static IResult NoContentOrNotFound(bool succeeded, string message)
        => succeeded ? Results.NoContent() : NotFound(message);
}
