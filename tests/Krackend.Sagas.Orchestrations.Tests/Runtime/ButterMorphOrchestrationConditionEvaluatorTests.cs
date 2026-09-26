namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using global::ButterMorph.Abstractions;
using global::ButterMorph.Core;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.ButterMorph;
using Krackend.Sagas.Orchestrations.Runtime.ButterMorph.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Conditions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

public sealed class ButterMorphOrchestrationConditionEvaluatorTests
{
    [Fact]
    public async Task EvaluateAsync_WhenConditionIsDisabled_ReturnsTrue()
    {
        var result = await CreateEvaluator().EvaluateAsync(new OrchestrationConditionEvaluationRequest
        {
            Condition = DisabledCondition(),
            PayloadContext = EmptyPayloadContext(),
            ElementKey = "reserve-inventory",
            Phase = "Task"
        });

        Assert.True(result.Succeeded);
        Assert.True(result.ShouldExecute);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public async Task EvaluateAsync_WhenConditionIsLiteral_ReturnsLiteralValue(string expression, bool expected)
    {
        var result = await CreateEvaluator().EvaluateAsync(new OrchestrationConditionEvaluationRequest
        {
            Condition = EnabledCondition(expression),
            PayloadContext = EmptyPayloadContext(),
            ElementKey = "reserve-inventory",
            Phase = "Task"
        });

        Assert.True(result.Succeeded);
        Assert.Equal(expected, result.ShouldExecute);
    }

    [Fact]
    public async Task EvaluateAsync_WhenConditionHasNoDslConfiguration_ReturnsConfigurationNotSupported()
    {
        var result = await CreateEvaluator().EvaluateAsync(new OrchestrationConditionEvaluationRequest
        {
            Condition = new ExecutionConditionArtifact(EngineType.DSL, null!) { IsEnabled = true },
            PayloadContext = EmptyPayloadContext(),
            ElementKey = "reserve-inventory",
            Phase = "Task"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("ConditionConfigurationNotSupported", result.ErrorCode);
    }

    [Fact]
    public async Task EvaluateAsync_WhenConditionExpressionIsMissing_ReturnsExpressionMissing()
    {
        var result = await CreateEvaluator().EvaluateAsync(new OrchestrationConditionEvaluationRequest
        {
            Condition = EnabledCondition(string.Empty),
            PayloadContext = EmptyPayloadContext(),
            ElementKey = "reserve-inventory",
            Phase = "Task"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("ConditionExpressionMissing", result.ErrorCode);
    }

    [Fact]
    public async Task EvaluateAsync_WhenConditionReadsPayload_ReturnsEvaluatedValue()
    {
        var result = await CreateEvaluator().EvaluateAsync(new OrchestrationConditionEvaluationRequest
        {
            Condition = EnabledCondition("$trigger.CanReserve"),
            PayloadContext = new OrchestrationPayloadContext
            {
                ContextPayload = JsonNode.Parse(
                    """
                    {
                      "trigger": {
                        "payload": {
                          "SaleId": "sale-1",
                          "CanReserve": true
                        }
                      },
                      "stages": {},
                      "variables": {}
                    }
                    """)!,
                TriggerPayload = JsonNode.Parse("""{"SaleId":"sale-1","CanReserve":true}""")!,
                StageKey = "inventory-reservation",
                TaskKey = "inventories.reserve"
            },
            ElementKey = "reserve-inventory",
            Phase = "Task"
        });

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(result.ShouldExecute);
    }

    [Fact]
    public async Task EvaluateAsync_WhenConditionIsButterMorphValidationDslAndIsValid_ReturnsTrue()
    {
        var engine = Substitute.For<IButterMorphEngine>();
        var parser = Substitute.For<IDslParser>();
        var sourceGraphBuilder = Substitute.For<IButterMorphSourceGraphBuilder>();
        var contextGraph = Substitute.For<IStructureGraph>();
        parser.Parse(Arg.Any<IDslDefinition>()).Returns(new DslDocument());
        sourceGraphBuilder.Build(Arg.Any<OrchestrationPayloadContext>())
            .Returns(new Dictionary<string, IStructureGraph>(StringComparer.Ordinal)
            {
                ["context"] = contextGraph
            });
        engine.Validate(Arg.Any<ValidationRequest>())
            .Returns(new ValidationResult { IsValid = true });
        var evaluator = new ButterMorphOrchestrationConditionEvaluator(
            engine,
            parser,
            new ButterMorphDiagnosticMetadataMapper(),
            sourceGraphBuilder);

        var result = await evaluator.EvaluateAsync(new OrchestrationConditionEvaluationRequest
        {
            Condition = EnabledCondition("validate { assert context.trigger.payload.CanReserve == true }"),
            PayloadContext = EmptyPayloadContext(),
            ElementKey = "reserve-inventory",
            Phase = "Task"
        });

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(result.ShouldExecute);
        engine.Received(1).Validate(Arg.Is<ValidationRequest>(request =>
            ReferenceEquals(request.SourceGraph, contextGraph) &&
            request.PayloadAlias == "context"));
    }

    [Fact]
    public async Task EvaluateAsync_WhenConditionIsButterMorphValidationDslAndIsInvalid_ReturnsFalse()
    {
        var engine = Substitute.For<IButterMorphEngine>();
        var parser = Substitute.For<IDslParser>();
        var sourceGraphBuilder = Substitute.For<IButterMorphSourceGraphBuilder>();
        parser.Parse(Arg.Any<IDslDefinition>()).Returns(new DslDocument());
        sourceGraphBuilder.Build(Arg.Any<OrchestrationPayloadContext>())
            .Returns(new Dictionary<string, IStructureGraph>(StringComparer.Ordinal));
        engine.Validate(Arg.Any<ValidationRequest>())
            .Returns(new ValidationResult { IsValid = false });
        var evaluator = new ButterMorphOrchestrationConditionEvaluator(
            engine,
            parser,
            new ButterMorphDiagnosticMetadataMapper(),
            sourceGraphBuilder);

        var result = await evaluator.EvaluateAsync(new OrchestrationConditionEvaluationRequest
        {
            Condition = EnabledCondition("validate { assert context.trigger.payload.CanReserve == true }"),
            PayloadContext = EmptyPayloadContext(),
            ElementKey = "reserve-inventory",
            Phase = "Task"
        });

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.False(result.ShouldExecute);
        engine.Received(1).Validate(Arg.Any<ValidationRequest>());
    }

    [Theory]
    [InlineData("\"true\"", true)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public async Task EvaluateAsync_WhenConditionReturnsStringOrNumber_CoercesToBoolean(
        string expression,
        bool expected)
    {
        var result = await CreateEvaluator().EvaluateAsync(new OrchestrationConditionEvaluationRequest
        {
            Condition = EnabledCondition(expression),
            PayloadContext = EmptyPayloadContext(),
            ElementKey = "reserve-inventory",
            Phase = "Task"
        });

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(expected, result.ShouldExecute);
    }

    [Fact]
    public async Task EvaluateAsync_WhenConditionResultIsNotBoolean_ReturnsResultNotBoolean()
    {
        var result = await CreateEvaluator().EvaluateAsync(new OrchestrationConditionEvaluationRequest
        {
            Condition = EnabledCondition("\"pending\""),
            PayloadContext = EmptyPayloadContext(),
            ElementKey = "reserve-inventory",
            Phase = "Task"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("ConditionResultNotBoolean", result.ErrorCode);
    }

    [Fact]
    public async Task EvaluateAsync_WhenDslCannotExecute_ReturnsExecutionFailedWithDiagnostics()
    {
        var result = await CreateEvaluator().EvaluateAsync(new OrchestrationConditionEvaluationRequest
        {
            Condition = EnabledCondition("$missing."),
            PayloadContext = EmptyPayloadContext(),
            ElementKey = "reserve-inventory",
            Phase = "Task"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("ConditionEvaluationFailed", result.ErrorCode);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public async Task EvaluateAsync_WhenButterMorphThrows_ReturnsExecutionFailedWithExceptionMetadata()
    {
        var engine = Substitute.For<IButterMorphEngine>();
        var parser = Substitute.For<IDslParser>();
        parser.Parse(Arg.Any<IDslDefinition>()).Returns(_ => throw new InvalidOperationException("invalid condition dsl"));
        var evaluator = new ButterMorphOrchestrationConditionEvaluator(
            engine,
            parser,
            new ButterMorphDiagnosticMetadataMapper(),
            Substitute.For<IButterMorphSourceGraphBuilder>());

        var result = await evaluator.EvaluateAsync(new OrchestrationConditionEvaluationRequest
        {
            Condition = EnabledCondition("$trigger.CanReserve"),
            PayloadContext = EmptyPayloadContext(),
            ElementKey = "reserve-inventory",
            Phase = "Task"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("ConditionExecutionFailed", result.ErrorCode);
        Assert.True(result.Diagnostics.ContainsKey("exceptionType"));
    }

    private static IOrchestrationConditionEvaluator CreateEvaluator()
    {
        var services = new ServiceCollection();
        services.AddKrackendOrchestrationsRuntimeButterMorph();
        return services
            .BuildServiceProvider()
            .GetRequiredService<IOrchestrationConditionEvaluator>();
    }

    private static OrchestrationPayloadContext EmptyPayloadContext()
        => new()
        {
            ContextPayload = JsonNode.Parse("""{"trigger":{"payload":{}},"stages":{},"variables":{}}""")!,
            TriggerPayload = JsonNode.Parse("{}")!,
            StageKey = "inventory-reservation",
            TaskKey = "inventories.reserve"
        };

    private static ExecutionConditionArtifact DisabledCondition()
        => new(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression(string.Empty)))
        {
            IsEnabled = false
        };

    private static ExecutionConditionArtifact EnabledCondition(string expression)
        => new(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression(expression)))
        {
            IsEnabled = true
        };
}
