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

        var errorCode = mapper.Resolve(new TimeoutException("The remote service timed out."));

        Assert.Equal("RemoteTimeout", errorCode);
    }

    [Fact]
    public void ResolveUsesMostSpecificMapping()
    {
        var mapper = CreateMapper(options =>
        {
            options.Map<Exception>("GenericFailure");
            options.Map<InvalidOperationException>("InvalidOperation");
        });

        var errorCode = mapper.Resolve(new InvalidOperationException("Invalid state."));

        Assert.Equal("InvalidOperation", errorCode);
    }

    [Fact]
    public void ResolveUsesDefaultCodeWhenNoMappingMatches()
    {
        var mapper = CreateMapper(options =>
            options.DefaultErrorCode = "UnknownClientFailure");

        var errorCode = mapper.Resolve(new NotSupportedException("Not supported."));

        Assert.Equal("UnknownClientFailure", errorCode);
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
