namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using global::ButterMorph.Abstractions;
using global::ButterMorph.Core;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.ButterMorph;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using RuntimeButterMorphServices = Krackend.Sagas.Orchestrations.Runtime.ButterMorph.DependencyInjection.ServiceCollectionExtensions;

public sealed class ButterMorphOrchestrationValidationExecutorTests
{
    [Fact]
    public async Task ValidateAsyncSucceedsWhenNoValidationDslAndSchemaValidationIsDisabled()
    {
        var executor = CreateExecutor();

        var result = await executor.ValidateAsync(new()
        {
            Phase = "Request",
            Payload = JsonNode.Parse("""{"saleId":"sale-1"}"""),
            SchemaBinding = new SchemaBindingArtifact(
                Id.New(),
                ElementType.Task,
                Id.New(),
                Id.New(),
                "inventories.reserve.request",
                new SemanticVersion(1, 0, 0),
                Id.New(),
                false)
            {
                ContractKind = SchemaContractKind.CommandRequest,
                IsValidationEnabled = false
            }
        });

        Assert.True(result.Succeeded);

        var noSchemaResult = await executor.ValidateAsync(new()
        {
            Phase = "Request",
            Payload = null,
            SchemaBinding = null
        });

        Assert.True(noSchemaResult.Succeeded);
    }

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

    [Fact]
    public async Task ValidateAsyncMapsButterMorphInvalidResultToRuntimeFailure()
    {
        var engine = Substitute.For<IButterMorphEngine>();
        var parser = Substitute.For<IDslParser>();
        parser.Parse(Arg.Any<IDslDefinition>()).Returns(new DslDocument());
        engine.Validate(Arg.Any<ValidationRequest>())
            .Returns(new ValidationResult
            {
                IsValid = false,
                Diagnostics =
                [
                    new DiagnosticEntry
                    {
                        Code = "required",
                        Message = "saleId is required",
                        Path = "$.saleId",
                        Severity = "Error"
                    }
                ]
            });
        var executor = new ButterMorphOrchestrationValidationExecutor(
            engine,
            parser,
            new ButterMorphDiagnosticMetadataMapper(),
            Substitute.For<IButterMorphSourceGraphBuilder>());

        var result = await executor.ValidateAsync(new OrchestrationValidationRequest
        {
            Phase = "Request",
            Payload = JsonNode.Parse("""{"total":10}""")!,
            ValidationDsl = "validate sale"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("RequestValidationFailed", result.ErrorCode);
        Assert.True(result.Diagnostics.ContainsKey("diagnostics"));
    }

    [Fact]
    public async Task ValidateAsyncMapsButterMorphExceptionsToExecutionFailure()
    {
        var engine = Substitute.For<IButterMorphEngine>();
        var parser = Substitute.For<IDslParser>();
        parser.Parse(Arg.Any<IDslDefinition>()).Returns(_ => throw new InvalidOperationException("invalid validation dsl"));
        var executor = new ButterMorphOrchestrationValidationExecutor(
            engine,
            parser,
            new ButterMorphDiagnosticMetadataMapper(),
            Substitute.For<IButterMorphSourceGraphBuilder>());

        var result = await executor.ValidateAsync(new OrchestrationValidationRequest
        {
            Phase = "Response",
            Payload = JsonNode.Parse("""{"reserved":true}""")!,
            ValidationDsl = "bad dsl"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("ResponseValidationExecutionFailed", result.ErrorCode);
        Assert.True(result.Diagnostics.ContainsKey("exceptionType"));
    }

    [Fact]
    public async Task ValidateAsyncUsesAccumulatedSourcesWhenPayloadContextIsProvided()
    {
        var engine = Substitute.For<IButterMorphEngine>();
        var parser = Substitute.For<IDslParser>();
        var sourceGraphBuilder = Substitute.For<IButterMorphSourceGraphBuilder>();
        var contextGraph = Substitute.For<IStructureGraph>();
        var triggerGraph = Substitute.For<IStructureGraph>();
        var sources = new Dictionary<string, IStructureGraph>(StringComparer.OrdinalIgnoreCase)
        {
            ["context"] = contextGraph,
            ["trigger"] = triggerGraph
        };

        parser.Parse(Arg.Any<IDslDefinition>()).Returns(new DslDocument());
        sourceGraphBuilder.Build(Arg.Any<OrchestrationPayloadContext>()).Returns(sources);
        engine.Validate(Arg.Any<ValidationRequest>()).Returns(new ValidationResult { IsValid = true });
        var executor = new ButterMorphOrchestrationValidationExecutor(
            engine,
            parser,
            new ButterMorphDiagnosticMetadataMapper(),
            sourceGraphBuilder);

        var result = await executor.ValidateAsync(new OrchestrationValidationRequest
        {
            Phase = "TaskEntry",
            PayloadContext = new OrchestrationPayloadContext
            {
                ContextPayload = JsonNode.Parse("""{"trigger":{"payload":{"saleId":"sale-1"}}}"""),
                TriggerPayload = JsonNode.Parse("""{"saleId":"sale-1"}"""),
                StageKey = "fulfillment",
                TaskKey = "inventory_reservation"
            },
            PayloadAlias = "context",
            ValidationDsl = "validate task"
        });

        Assert.True(result.Succeeded);
        engine.Received(1).Validate(Arg.Is<ValidationRequest>(request =>
            ReferenceEquals(request.Sources, sources) &&
            ReferenceEquals(request.SourceGraph, contextGraph) &&
            request.PayloadAlias == "context"));

        var blankAliasResult = await executor.ValidateAsync(new OrchestrationValidationRequest
        {
            Phase = "TaskEntry",
            PayloadContext = new OrchestrationPayloadContext
            {
                ContextPayload = JsonNode.Parse("""{"trigger":{"payload":{"saleId":"sale-1"}}}"""),
                TriggerPayload = JsonNode.Parse("""{"saleId":"sale-1"}"""),
                StageKey = "fulfillment",
                TaskKey = "inventory_reservation"
            },
            PayloadAlias = " ",
            ValidationDsl = "validate task"
        });

        Assert.True(blankAliasResult.Succeeded);
        engine.Received(2).Validate(Arg.Is<ValidationRequest>(request =>
            request.PayloadAlias == "context" &&
            ReferenceEquals(request.SourceGraph, contextGraph)));
    }

    [Fact]
    public async Task ValidateAsyncUsesEmptyJsonPayloadWhenPayloadAndContextAreMissing()
    {
        var engine = Substitute.For<IButterMorphEngine>();
        var parser = Substitute.For<IDslParser>();
        parser.Parse(Arg.Any<IDslDefinition>()).Returns(new DslDocument());
        engine.Validate(Arg.Any<ValidationRequest>()).Returns(new ValidationResult { IsValid = true });
        var executor = new ButterMorphOrchestrationValidationExecutor(
            engine,
            parser,
            new ButterMorphDiagnosticMetadataMapper(),
            Substitute.For<IButterMorphSourceGraphBuilder>());

        var result = await executor.ValidateAsync(new OrchestrationValidationRequest
        {
            Phase = "Request",
            Payload = null,
            ValidationDsl = "validate request"
        });

        Assert.True(result.Succeeded);
        engine.Received(1).Validate(Arg.Any<ValidationRequest>());
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
