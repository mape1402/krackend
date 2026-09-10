namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.ButterMorph;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Microsoft.Extensions.DependencyInjection;
using RuntimeButterMorphServices = Krackend.Sagas.Orchestrations.Runtime.ButterMorph.DependencyInjection.ServiceCollectionExtensions;

public sealed class ButterMorphOrchestrationValidationExecutorTests
{
    [Fact]
    public async Task ValidateAsyncFailsWhenSchemaValidationIsEnabledWithoutExecutableDsl()
    {
        var executor = CreateExecutor();
        var result = await executor.ValidateAsync(new()
        {
            Phase = "Request",
            SchemaBinding = new SchemaBindingArtifact(
                Id.New(),
                ElementType.Task,
                Id.New(),
                Id.New(),
                "inventories.reserve.request",
                new SemanticVersion(1, 0, 0),
                Id.New(),
                true)
            {
                ContractKind = SchemaContractKind.CommandRequest,
                IsValidationEnabled = true
            }
        });

        Assert.False(result.Succeeded);
        Assert.Equal("RequestSchemaValidationNotConfigured", result.ErrorCode);
    }

    private static ButterMorphOrchestrationValidationExecutor CreateExecutor()
    {
        var services = new ServiceCollection();
        RuntimeButterMorphServices.AddKrackendOrchestrationsRuntimeButterMorph(services);
        return (ButterMorphOrchestrationValidationExecutor)services
            .BuildServiceProvider()
            .GetRequiredService<Krackend.Sagas.Orchestrations.Runtime.Engine.Validation.IOrchestrationValidationExecutor>();
    }
}
