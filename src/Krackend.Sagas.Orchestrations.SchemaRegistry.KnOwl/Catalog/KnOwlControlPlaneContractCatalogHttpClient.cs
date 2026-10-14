using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KnOwl.Contracts.Artifacts;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;

/// <summary>
/// HTTP client for the KnOwl Control Plane deployed contract catalog.
/// </summary>
public sealed class KnOwlControlPlaneContractCatalogHttpClient : IKnOwlControlPlaneContractCatalogClient
{
    private readonly HttpClient _httpClient;
    private readonly IKnOwlControlPlaneCatalogRequestConfigurator _requestConfigurator;
    private readonly KnOwlSchemaRegistryOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnOwlControlPlaneContractCatalogHttpClient"/> class.
    /// </summary>
    public KnOwlControlPlaneContractCatalogHttpClient(HttpClient httpClient)
        : this(httpClient, Options.Create(new KnOwlSchemaRegistryOptions()), new DefaultKnOwlControlPlaneCatalogRequestConfigurator())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KnOwlControlPlaneContractCatalogHttpClient"/> class.
    /// </summary>
    public KnOwlControlPlaneContractCatalogHttpClient(
        HttpClient httpClient,
        IOptions<KnOwlSchemaRegistryOptions> options,
        IKnOwlControlPlaneCatalogRequestConfigurator requestConfigurator)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? new KnOwlSchemaRegistryOptions();
        _requestConfigurator = requestConfigurator ?? throw new ArgumentNullException(nameof(requestConfigurator));
    }

    /// <inheritdoc />
    public async Task<KnOwlContractCatalogResult> GetAllDeployedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await SendJsonRequestAsync(NormalizePath(_options.CatalogPath, KnOwlSchemaRegistryOptions.DefaultCatalogPath), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return MapHttpFailure(response.StatusCode);
            }

            if (!IsJsonResponse(response))
            {
                return MapNonJsonSuccess();
            }

            var documents = await response.Content.ReadFromJsonAsync<KnOwlContractArtifactDocument[]>(cancellationToken)
                ?? Array.Empty<KnOwlContractArtifactDocument>();
            return KnOwlContractCatalogResult.FoundMany(documents
                .Where(document => document is not null)
                .Select(document => document.ToContractArtifact())
                .ToArray());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return KnOwlContractCatalogResult.Failed(KnOwlContractCatalogStatus.Unavailable, "KnOwl Control Plane catalog request timed out.");
        }
        catch (HttpRequestException exception)
        {
            return KnOwlContractCatalogResult.Failed(KnOwlContractCatalogStatus.Unavailable, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return KnOwlContractCatalogResult.Failed(KnOwlContractCatalogStatus.Unavailable, exception.Message);
        }
        catch (JsonException exception)
        {
            return KnOwlContractCatalogResult.Failed(KnOwlContractCatalogStatus.Unavailable, $"KnOwl Control Plane catalog returned invalid JSON: {exception.Message}");
        }
        catch (NotSupportedException exception)
        {
            return KnOwlContractCatalogResult.Failed(KnOwlContractCatalogStatus.Unavailable, $"KnOwl Control Plane catalog returned unsupported content: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public Task<KnOwlContractCatalogResult> GetExactAsync(
        ContractArtifactType artifactType,
        string topic,
        string versionNumber,
        CancellationToken cancellationToken = default)
        => artifactType switch
        {
            ContractArtifactType.Event => GetSingleAsync(
                BuildPath(
                    _options.EventContractPath,
                    KnOwlSchemaRegistryOptions.DefaultEventContractPath,
                    topic,
                    versionNumber),
                cancellationToken),
            ContractArtifactType.CommandRequest or ContractArtifactType.CommandReply => GetCommandSideAsync(
                BuildPath(
                    _options.CommandContractPath,
                    KnOwlSchemaRegistryOptions.DefaultCommandContractPath,
                    topic,
                    versionNumber),
                artifactType,
                cancellationToken),
            _ => Task.FromResult(KnOwlContractCatalogResult.Failed(
                KnOwlContractCatalogStatus.Invalid,
                $"KnOwl Control Plane catalog does not support artifact type '{artifactType}'."))
        };

    /// <inheritdoc />
    public Task<KnOwlContractCatalogResult> GetLatestAsync(
        ContractArtifactType artifactType,
        string topic,
        CancellationToken cancellationToken = default)
        => artifactType switch
        {
            ContractArtifactType.Event => GetSingleAsync(
                BuildPath(
                    _options.LatestEventContractPath,
                    KnOwlSchemaRegistryOptions.DefaultLatestEventContractPath,
                    topic,
                    string.Empty),
                cancellationToken),
            ContractArtifactType.CommandRequest or ContractArtifactType.CommandReply => GetCommandSideAsync(
                BuildPath(
                    _options.LatestCommandContractPath,
                    KnOwlSchemaRegistryOptions.DefaultLatestCommandContractPath,
                    topic,
                    string.Empty),
                artifactType,
                cancellationToken),
            _ => Task.FromResult(KnOwlContractCatalogResult.Failed(
                KnOwlContractCatalogStatus.Invalid,
                $"KnOwl Control Plane catalog does not support artifact type '{artifactType}'."))
        };

    /// <inheritdoc />
    public Task<KnOwlCommandContractCatalogResult> GetExactCommandAsync(
        string commandKey,
        string versionNumber,
        CancellationToken cancellationToken = default)
        => GetCommandAsync(
            BuildPath(
                _options.CommandContractPath,
                KnOwlSchemaRegistryOptions.DefaultCommandContractPath,
                commandKey,
                versionNumber),
            cancellationToken);

    /// <inheritdoc />
    public Task<KnOwlCommandContractCatalogResult> GetLatestCommandAsync(
        string commandKey,
        CancellationToken cancellationToken = default)
        => GetCommandAsync(
            BuildPath(
                _options.LatestCommandContractPath,
                KnOwlSchemaRegistryOptions.DefaultLatestCommandContractPath,
                commandKey,
                string.Empty),
            cancellationToken);

    private async Task<KnOwlContractCatalogResult> GetSingleAsync(string requestUri, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendJsonRequestAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return MapHttpFailure(response.StatusCode);
            }

            if (!IsJsonResponse(response))
            {
                return MapNonJsonSuccess();
            }

            var document = await response.Content.ReadFromJsonAsync<KnOwlContractArtifactDocument>(cancellationToken);
            var contract = document?.ToContractArtifact();
            return contract is null
                ? KnOwlContractCatalogResult.Failed(KnOwlContractCatalogStatus.NotFound, "KnOwl Control Plane catalog returned no contract artifact.")
                : KnOwlContractCatalogResult.Found(contract);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return KnOwlContractCatalogResult.Failed(KnOwlContractCatalogStatus.Unavailable, "KnOwl Control Plane catalog request timed out.");
        }
        catch (HttpRequestException exception)
        {
            return KnOwlContractCatalogResult.Failed(KnOwlContractCatalogStatus.Unavailable, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return KnOwlContractCatalogResult.Failed(KnOwlContractCatalogStatus.Unavailable, exception.Message);
        }
        catch (JsonException exception)
        {
            return KnOwlContractCatalogResult.Failed(KnOwlContractCatalogStatus.Unavailable, $"KnOwl Control Plane catalog returned invalid JSON: {exception.Message}");
        }
        catch (NotSupportedException exception)
        {
            return KnOwlContractCatalogResult.Failed(KnOwlContractCatalogStatus.Unavailable, $"KnOwl Control Plane catalog returned unsupported content: {exception.Message}");
        }
    }

    private async Task<KnOwlContractCatalogResult> GetCommandSideAsync(
        string requestUri,
        ContractArtifactType artifactType,
        CancellationToken cancellationToken)
    {
        var result = await GetCommandAsync(requestUri, cancellationToken);
        if (result.Status != KnOwlContractCatalogStatus.Found)
        {
            return KnOwlContractCatalogResult.Failed(result.Status, result.Message);
        }

        var artifact = artifactType == ContractArtifactType.CommandReply
            ? result.Command?.ReplyArtifact
            : result.Command?.RequestArtifact;

        return artifact is null
            ? KnOwlContractCatalogResult.Failed(
                KnOwlContractCatalogStatus.NotFound,
                $"KnOwl Control Plane catalog returned no '{artifactType}' artifact for the requested command.")
            : KnOwlContractCatalogResult.Found(artifact);
    }

    private async Task<KnOwlCommandContractCatalogResult> GetCommandAsync(
        string requestUri,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendJsonRequestAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var failure = MapHttpFailure(response.StatusCode);
                return KnOwlCommandContractCatalogResult.Failed(failure.Status, failure.Message);
            }

            if (!IsJsonResponse(response))
            {
                var failure = MapNonJsonSuccess();
                return KnOwlCommandContractCatalogResult.Failed(failure.Status, failure.Message);
            }

            var document = await response.Content.ReadFromJsonAsync<KnOwlCommandContractArtifactsDocument>(cancellationToken);
            if (document?.RequestArtifact is null)
            {
                return KnOwlCommandContractCatalogResult.Failed(KnOwlContractCatalogStatus.NotFound, "KnOwl Control Plane catalog returned no command request artifact.");
            }

            var command = document?.ToCommandContractArtifacts();
            return command is null
                ? KnOwlCommandContractCatalogResult.Failed(KnOwlContractCatalogStatus.NotFound, "KnOwl Control Plane catalog returned no command contract artifacts.")
                : KnOwlCommandContractCatalogResult.Found(command);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return KnOwlCommandContractCatalogResult.Failed(KnOwlContractCatalogStatus.Unavailable, "KnOwl Control Plane catalog request timed out.");
        }
        catch (HttpRequestException exception)
        {
            return KnOwlCommandContractCatalogResult.Failed(KnOwlContractCatalogStatus.Unavailable, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return KnOwlCommandContractCatalogResult.Failed(KnOwlContractCatalogStatus.Unavailable, exception.Message);
        }
        catch (JsonException exception)
        {
            return KnOwlCommandContractCatalogResult.Failed(KnOwlContractCatalogStatus.Unavailable, $"KnOwl Control Plane catalog returned invalid JSON: {exception.Message}");
        }
        catch (NotSupportedException exception)
        {
            return KnOwlCommandContractCatalogResult.Failed(KnOwlContractCatalogStatus.Unavailable, $"KnOwl Control Plane catalog returned unsupported content: {exception.Message}");
        }
    }

    private static KnOwlContractCatalogResult MapHttpFailure(HttpStatusCode statusCode)
        => statusCode switch
        {
            HttpStatusCode.NotFound => KnOwlContractCatalogResult.Failed(
                KnOwlContractCatalogStatus.NotFound,
                "KnOwl Control Plane catalog did not find the requested deployed contract artifact."),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => KnOwlContractCatalogResult.Failed(
                KnOwlContractCatalogStatus.Unavailable,
                $"KnOwl Control Plane catalog requires authentication or authorization. HTTP {(int)statusCode}."),
            HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect => KnOwlContractCatalogResult.Failed(
                KnOwlContractCatalogStatus.Unavailable,
                $"KnOwl Control Plane catalog redirected the request. The endpoint may require authentication or a different BaseUri. HTTP {(int)statusCode}."),
            _ => KnOwlContractCatalogResult.Failed(
                KnOwlContractCatalogStatus.Unavailable,
                $"KnOwl Control Plane catalog returned HTTP {(int)statusCode}.")
        };

    private async Task<HttpResponseMessage> SendJsonRequestAsync(string requestUri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        await _requestConfigurator.ConfigureAsync(request, cancellationToken);
        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private static bool IsJsonResponse(HttpResponseMessage response)
    {
        if (response.Content is null)
        {
            return false;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        return string.IsNullOrWhiteSpace(mediaType) ||
            mediaType.Contains("json", StringComparison.OrdinalIgnoreCase);
    }

    private static KnOwlContractCatalogResult MapNonJsonSuccess()
        => KnOwlContractCatalogResult.Failed(
            KnOwlContractCatalogStatus.Unavailable,
            "KnOwl Control Plane catalog returned non-JSON content. The endpoint may require authentication or the configured BaseUri may point to a browser page.");

    private static string BuildPath(
        string template,
        string defaultTemplate,
        string contractKey,
        string versionNumber)
    {
        var path = NormalizePath(template, defaultTemplate);
        var escapedKey = Uri.EscapeDataString(contractKey ?? string.Empty);
        var escapedVersion = Uri.EscapeDataString(versionNumber ?? string.Empty);
        return path
            .Replace("{contractKey}", escapedKey, StringComparison.OrdinalIgnoreCase)
            .Replace("{topic}", escapedKey, StringComparison.OrdinalIgnoreCase)
            .Replace("{versionNumber}", escapedVersion, StringComparison.OrdinalIgnoreCase)
            .Replace("{version}", escapedVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path, string defaultPath)
        => (string.IsNullOrWhiteSpace(path) ? defaultPath : path.Trim()).TrimStart('/');
}
