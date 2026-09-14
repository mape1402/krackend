namespace Krackend.Sagas.Orchestrations.Tests.Client;

using Krackend.Sagas.Orchestrations.Client.Errors;
using Microsoft.Extensions.DependencyInjection;

public sealed class OrchestrationExceptionErrorCodeMapperTests
{
    [Fact]
    public void ResolveUsesConfiguredExceptionCode()
    {
        var mapper = CreateMapper(options =>
            options.Map<TimeoutException>("RemoteTimeout"));

        var resolution = mapper.Resolve(new TimeoutException("The remote service timed out."));

        Assert.Equal("RemoteTimeout", resolution.ErrorCode);
        Assert.Null(resolution.IsRetryableCandidate);
    }

    [Fact]
    public void ResolveUsesMostSpecificMapping()
    {
        var mapper = CreateMapper(options =>
        {
            options.Map<Exception>("GenericFailure");
            options.Map<InvalidOperationException>("InvalidOperation");
        });

        var resolution = mapper.Resolve(new InvalidOperationException("Invalid state."));

        Assert.Equal("InvalidOperation", resolution.ErrorCode);
    }

    [Fact]
    public void ResolveUsesDefaultCodeWhenNoMappingMatches()
    {
        var mapper = CreateMapper(options =>
            options.DefaultErrorCode = "UnknownClientFailure");

        var resolution = mapper.Resolve(new NotSupportedException("Not supported."));

        Assert.Equal("UnknownClientFailure", resolution.ErrorCode);
    }

    [Fact]
    public void ResolveUsesPredicateMappingWhenItMatches()
    {
        var mapper = CreateMapper(options =>
        {
            options.Map<InvalidOperationException>(
                "InventoryTemporaryFailure",
                exception => exception.Message.Contains("temporary", StringComparison.OrdinalIgnoreCase),
                isRetryableCandidate: true);
            options.Map<InvalidOperationException>("InventoryPermanentFailure", isRetryableCandidate: false);
        });

        var resolution = mapper.Resolve(new InvalidOperationException("Temporary inventory outage."));

        Assert.Equal("InventoryTemporaryFailure", resolution.ErrorCode);
        Assert.True(resolution.IsRetryableCandidate);
    }

    [Fact]
    public void ResolveFallsBackToTypeMappingWhenPredicateDoesNotMatch()
    {
        var mapper = CreateMapper(options =>
        {
            options.Map<InvalidOperationException>(
                "InventoryTemporaryFailure",
                exception => exception.Message.Contains("temporary", StringComparison.OrdinalIgnoreCase),
                isRetryableCandidate: true);
            options.Map<InvalidOperationException>("InventoryPermanentFailure", isRetryableCandidate: false);
        });

        var resolution = mapper.Resolve(new InvalidOperationException("Permanent validation failure."));

        Assert.Equal("InventoryPermanentFailure", resolution.ErrorCode);
        Assert.False(resolution.IsRetryableCandidate);
    }

    private static IOrchestrationExceptionErrorCodeMapper CreateMapper(
        Action<OrchestrationClientErrorMappingOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddKrackendOrchestrationsClient(configure);

        return services
            .BuildServiceProvider()
            .GetRequiredService<IOrchestrationExceptionErrorCodeMapper>();
    }
}
