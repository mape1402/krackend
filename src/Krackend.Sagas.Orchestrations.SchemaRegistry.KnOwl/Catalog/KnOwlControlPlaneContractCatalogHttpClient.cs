using System.Net;
using System.Net.Http.Json;
using KnOwl.Contracts.Artifacts;

namespace Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;

/// <summary>
/// HTTP client for the KnOwl Control Plane deployed contract catalog.
/// </summary>
public sealed class KnOwlControlPlaneContractCatalogHttpClient : IKnOwlControlPlaneContractCatalogClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnOwlControlPlaneContractCatalogHttpClient"/> class.
    /// </summary>
    public KnOwlControlPlaneContractCatalogHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <inheritdoc />
    public async Task<KnOwlContractCatalogResult> GetAllDeployedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("contracts/artifacts", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return MapHttpFailure(response.StatusCode);
            }

            var contracts = await response.Content.ReadFromJsonAsync<ContractArtifact[]>(cancellationToken)
                ?? Array.Empty<ContractArtifact>();
            return KnOwlContractCatalogResult.FoundMany(contracts);
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
                $"contracts/events/{Uri.EscapeDataString(topic ?? string.Empty)}/versions/{Uri.EscapeDataString(versionNumber ?? string.Empty)}",
                cancellationToken),
            ContractArtifactType.CommandRequest or ContractArtifactType.CommandReply => GetCommandSideAsync(
                $"contracts/commands/{Uri.EscapeDataString(topic ?? string.Empty)}/versions/{Uri.EscapeDataString(versionNumber ?? string.Empty)}",
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
                $"contracts/events/{Uri.EscapeDataString(topic ?? string.Empty)}/latest",
                cancellationToken),
            ContractArtifactType.CommandRequest or ContractArtifactType.CommandReply => GetCommandSideAsync(
                $"contracts/commands/{Uri.EscapeDataString(topic ?? string.Empty)}/latest",
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
            $"contracts/commands/{Uri.EscapeDataString(commandKey ?? string.Empty)}/versions/{Uri.EscapeDataString(versionNumber ?? string.Empty)}",
            cancellationToken);

    /// <inheritdoc />
    public Task<KnOwlCommandContractCatalogResult> GetLatestCommandAsync(
        string commandKey,
        CancellationToken cancellationToken = default)
        => GetCommandAsync(
            $"contracts/commands/{Uri.EscapeDataString(commandKey ?? string.Empty)}/latest",
            cancellationToken);

    private async Task<KnOwlContractCatalogResult> GetSingleAsync(string requestUri, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return MapHttpFailure(response.StatusCode);
            }

            var contract = await response.Content.ReadFromJsonAsync<ContractArtifact>(cancellationToken);
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
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var failure = MapHttpFailure(response.StatusCode);
                return KnOwlCommandContractCatalogResult.Failed(failure.Status, failure.Message);
            }

            var command = await response.Content.ReadFromJsonAsync<CommandContractArtifacts<ContractArtifact>>(cancellationToken);
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
    }

    private static KnOwlContractCatalogResult MapHttpFailure(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.NotFound
            ? KnOwlContractCatalogResult.Failed(KnOwlContractCatalogStatus.NotFound, "KnOwl Control Plane catalog did not find the requested deployed contract artifact.")
            : KnOwlContractCatalogResult.Failed(KnOwlContractCatalogStatus.Unavailable, $"KnOwl Control Plane catalog returned HTTP {(int)statusCode}.");
}
