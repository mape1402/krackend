using System.Net;
using System.Text;

namespace Krackend.Sagas.Orchestrations.Tests.SchemaRegistry;

internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

    public RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        _responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
    }

    public Uri RequestUri { get; private set; } = new("about:blank");

    public string LastAuthorizationScheme { get; private set; } = string.Empty;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestUri = request.RequestUri ?? new Uri("about:blank");
        LastAuthorizationScheme = request.Headers.Authorization?.Scheme ?? string.Empty;
        return Task.FromResult(_responseFactory(request));
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}
