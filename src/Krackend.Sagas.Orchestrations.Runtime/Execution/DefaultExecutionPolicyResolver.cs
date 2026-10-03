namespace Krackend.Sagas.Orchestrations.Runtime.Execution;

using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Abstractions.Extensions;
using Microsoft.Extensions.Options;

internal sealed class DefaultExecutionPolicyResolver : IExecutionPolicyResolver
{
    private readonly RuntimeExecutionOptions _options;
    private readonly IExecutionSandboxProviderRegistry _providerRegistry;

    public DefaultExecutionPolicyResolver(
        IOptions<RuntimeExecutionOptions> options,
        IExecutionSandboxProviderRegistry providerRegistry)
    {
        _options = options?.Value ?? new RuntimeExecutionOptions();
        _providerRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
    }

    public ExecutionPolicyResolutionResult Resolve(ExecutionPolicyResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Task);

        var capabilities = _options.RuntimeNodeCapabilities ?? RuntimeNodeCapabilitiesArtifact.LocalDefaults;
        var scopes = new[]
        {
            (Scope: ExecutionPolicyScope.Environment, Policy: _options.EnvironmentPolicy ?? ExecutionPolicyArtifact.Empty),
            (Scope: ExecutionPolicyScope.RuntimeNode, Policy: _options.RuntimeNodePolicy ?? ExecutionPolicyArtifact.Empty),
            (Scope: ExecutionPolicyScope.Orchestration, Policy: request.OrchestrationPolicy ?? ExecutionPolicyArtifact.Empty),
            (Scope: ExecutionPolicyScope.Stage, Policy: request.StagePolicy ?? ExecutionPolicyArtifact.Empty),
            (Scope: ExecutionPolicyScope.Task, Policy: request.Task.ExecutionPolicy ?? ExecutionPolicyArtifact.Empty)
        };

        var selectedProvider = string.Empty;
        var selectedProviderSource = ExecutionPolicyScope.Environment;
        var allowedProviders = Normalize(capabilities.SupportedProviderKeys);
        var forbiddenProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requireSandboxForExternal = false;
        var isolation = ExecutionIsolationRequirement.None;
        int? timeoutSeconds = null;
        int? memoryMb = null;

        foreach (var (scope, policy) in scopes)
        {
            if (!string.IsNullOrWhiteSpace(policy.DefaultProviderKey))
            {
                selectedProvider = policy.DefaultProviderKey.Trim();
                selectedProviderSource = scope;
            }

            allowedProviders = IntersectAllowedProviders(allowedProviders, policy.AllowedProviderKeys);
            AddForbiddenProviders(forbiddenProviders, policy.ForbiddenProviderKeys);

            if (policy.RequireSandboxForExternalExtensions == true)
            {
                requireSandboxForExternal = true;
            }

            if (policy.Isolation.HasValue && policy.Isolation.Value > isolation)
            {
                isolation = policy.Isolation.Value;
            }

            timeoutSeconds = policy.TimeoutSeconds ?? timeoutSeconds;
            memoryMb = policy.MemoryMb ?? memoryMb;
        }

        selectedProvider = string.IsNullOrWhiteSpace(selectedProvider)
            ? FirstOrDefaultProvider(allowedProviders)
            : selectedProvider;

        if (string.IsNullOrWhiteSpace(selectedProvider))
        {
            return ExecutionPolicyResolutionResult.Failure(
                "ExecutionProviderMissing",
                $"No execution provider can be resolved for task '{request.Task.Key}' in stage '{request.StageKey}'.");
        }

        if (forbiddenProviders.Contains(selectedProvider))
        {
            return ExecutionPolicyResolutionResult.Failure(
                "ExecutionProviderForbidden",
                $"Execution provider '{selectedProvider}' is forbidden for task '{request.Task.Key}' in stage '{request.StageKey}'.");
        }

        if (allowedProviders.Count > 0 && !allowedProviders.Contains(selectedProvider))
        {
            return ExecutionPolicyResolutionResult.Failure(
                "ExecutionProviderNotAllowed",
                $"Execution provider '{selectedProvider}' is not allowed by the resolved policy for task '{request.Task.Key}' in stage '{request.StageKey}'.");
        }

        if (!_providerRegistry.TryGet(selectedProvider, out var provider))
        {
            return ExecutionPolicyResolutionResult.Failure(
                "ExecutionProviderNotConfigured",
                $"Execution provider '{selectedProvider}' is not configured on this runtime node.");
        }

        var requirements = request.Task.RuntimeRequirements ?? ExecutionRuntimeRequirementsArtifact.Empty;
        if (requirements.Isolation > isolation)
        {
            isolation = requirements.Isolation;
        }

        memoryMb = ResolveMemory(memoryMb, requirements);
        var supportedModes = Normalize(capabilities.SupportedExecutionModes);
        if (supportedModes.Count > 0 && !supportedModes.Contains(provider.ExecutionMode))
        {
            return ExecutionPolicyResolutionResult.Failure(
                "ExecutionModeNotSupported",
                $"Execution provider '{provider.ProviderKey}' uses mode '{provider.ExecutionMode}', which is not supported by this runtime node.");
        }

        var allowedModes = Normalize(requirements.AllowedExecutionModes);
        if (allowedModes.Count > 0 && !allowedModes.Contains(provider.ExecutionMode))
        {
            return ExecutionPolicyResolutionResult.Failure(
                "ExecutionModeNotAllowed",
                $"Task '{request.Task.Key}' does not allow execution mode '{provider.ExecutionMode}'.");
        }

        if (requirements.RequiresNetwork && !capabilities.SupportsNetwork)
        {
            return ExecutionPolicyResolutionResult.Failure(
                "ExecutionNetworkUnavailable",
                $"Task '{request.Task.Key}' requires network access, but this runtime node does not expose network-capable execution.");
        }

        var missingSecret = FindMissingSecret(requirements.RequiredSecrets, capabilities.AvailableSecrets);
        if (!string.IsNullOrWhiteSpace(missingSecret))
        {
            return ExecutionPolicyResolutionResult.Failure(
                "ExecutionSecretUnavailable",
                $"Task '{request.Task.Key}' requires secret '{missingSecret}', but this runtime node cannot resolve it.");
        }

        if (memoryMb.HasValue &&
            capabilities.MaxMemoryMb.HasValue &&
            memoryMb.Value > capabilities.MaxMemoryMb.Value)
        {
            return ExecutionPolicyResolutionResult.Failure(
                "ExecutionMemoryUnavailable",
                $"Task '{request.Task.Key}' requires {memoryMb.Value} MB, but this runtime node can assign at most {capabilities.MaxMemoryMb.Value} MB.");
        }

        var isExternalExtension = IsExternalExtension(request.Task.ExtensionKey);
        var sandboxRequired = isolation == ExecutionIsolationRequirement.Required ||
            (requireSandboxForExternal && isExternalExtension);

        if (sandboxRequired && !provider.IsSandbox)
        {
            return ExecutionPolicyResolutionResult.Failure(
                "ExecutionSandboxRequired",
                $"Task '{request.Task.Key}' must run in an execution sandbox, but provider '{provider.ProviderKey}' is not sandboxed.");
        }

        var resolved = new ResolvedExecutionPolicyArtifact(
            provider.ProviderKey,
            provider.ExecutionMode,
            selectedProviderSource,
            isolation)
        {
            TimeoutSeconds = timeoutSeconds,
            MemoryMb = memoryMb,
            RequireSandboxForExternalExtensions = requireSandboxForExternal
        };

        return ExecutionPolicyResolutionResult.Success(resolved, provider);
    }

    private static HashSet<string> IntersectAllowedProviders(
        HashSet<string> current,
        IReadOnlyList<string> next)
    {
        var normalizedNext = Normalize(next);
        if (normalizedNext.Count == 0)
        {
            return current;
        }

        if (current.Count == 0)
        {
            return normalizedNext;
        }

        current.IntersectWith(normalizedNext);
        return current;
    }

    private static void AddForbiddenProviders(
        ISet<string> forbiddenProviders,
        IReadOnlyList<string> configuredProviders)
    {
        if (configuredProviders is null)
        {
            return;
        }

        foreach (var provider in configuredProviders)
        {
            if (!string.IsNullOrWhiteSpace(provider))
            {
                forbiddenProviders.Add(provider.Trim());
            }
        }
    }

    private static HashSet<string> Normalize(IReadOnlyList<string> values)
        => values is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string FirstOrDefaultProvider(IReadOnlySet<string> providers)
        => providers.Count == 0
            ? ExecutionConstants.BuiltInLocalProvider
            : providers.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).First();

    private static int? ResolveMemory(
        int? policyMemoryMb,
        ExecutionRuntimeRequirementsArtifact requirements)
    {
        var memory = policyMemoryMb;
        if (requirements.MinMemoryMb.HasValue)
        {
            memory = Math.Max(memory ?? 0, requirements.MinMemoryMb.Value);
        }

        if (requirements.MaxMemoryMb.HasValue &&
            memory.HasValue &&
            memory.Value > requirements.MaxMemoryMb.Value)
        {
            memory = requirements.MaxMemoryMb.Value;
        }

        return memory;
    }

    private static string FindMissingSecret(
        IReadOnlyList<string> requiredSecrets,
        IReadOnlyList<string> availableSecrets)
    {
        var required = Normalize(requiredSecrets);
        if (required.Count == 0)
        {
            return string.Empty;
        }

        var available = Normalize(availableSecrets);
        if (available.Count == 0)
        {
            return string.Empty;
        }

        return required.FirstOrDefault(secret => !available.Contains(secret)) ?? string.Empty;
    }

    private static bool IsExternalExtension(string extensionKey)
        => !string.IsNullOrWhiteSpace(extensionKey) &&
            !string.Equals(extensionKey.Trim(), ExtensionConstants.BuiltInExtensionKey, StringComparison.OrdinalIgnoreCase);
}

