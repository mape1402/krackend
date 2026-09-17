namespace Krackend.Sagas.Orchestrations.Tests.Design;

using ButterMorph.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using DesignApplicationServices = Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.ServiceCollectionExtensions;

public sealed class OrchestrationArtifactDslValidationServiceTests
{
    [Fact]
    public void ValidateThrowsWhenEnabledTransformationHasNoDsl()
    {
        var service = CreateService();
        var version = CreateVersion();
        version.StageDefinitions[0].TaskDefinitions[0].HasTransformation = true;
        version.StageDefinitions[0].TaskDefinitions[0].Transformation = new TransformationDefinition
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration()
        };

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("stage:fulfillment:task:reserve-inventory:transformation", exception.Path);
    }

    [Fact]
    public void ValidateThrowsWhenEnabledTransformationHasNoDslConfiguration()
    {
        var service = CreateService();
        var version = CreateVersion();
        version.StageDefinitions[0].TaskDefinitions[0].HasTransformation = true;
        version.StageDefinitions[0].TaskDefinitions[0].Transformation = new TransformationDefinition
        {
            Engine = EngineType.DSL,
            Configuration = null!
        };

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("stage:fulfillment:task:reserve-inventory:transformation", exception.Path);
        Assert.Contains("must use ButterMorph DSL configuration", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateThrowsWhenEnabledRequestValidationHasNoDsl()
    {
        var service = CreateService();
        var version = CreateVersion();
        var messaging = (MessagingTaskConfiguration)version.StageDefinitions[0].TaskDefinitions[0].Configuration;
        messaging.HasRequestValidation = true;
        messaging.RequestValidation = new ValidationDefinition
        {
            Engine = EngineType.DSL,
            Configuration = new DslValidationConfiguration()
        };

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("stage:fulfillment:task:reserve-inventory:request-validation", exception.Path);
    }

    [Fact]
    public void ValidateThrowsWhenSchemaValidationHasNoDsl()
    {
        var service = CreateService();
        var version = CreateVersion();
        var messaging = (MessagingTaskConfiguration)version.StageDefinitions[0].TaskDefinitions[0].Configuration;
        messaging.HasSchemaValidation = true;

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("stage:fulfillment:task:reserve-inventory:request-validation", exception.Path);
    }

    [Fact]
    public void ValidateThrowsWhenResponseSchemaBindingRequiresValidationButDslIsMissing()
    {
        var service = CreateService();
        var version = CreateVersion();
        var messaging = (MessagingTaskConfiguration)version.StageDefinitions[0].TaskDefinitions[0].Configuration;
        messaging.ResponseSchemaBinding = ValidationEnabledBinding();

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("stage:fulfillment:task:reserve-inventory:response-validation", exception.Path);
    }

    [Fact]
    public void ValidateThrowsWhenEnabledStageConditionHasNoExpression()
    {
        var service = CreateService();
        var version = CreateVersion();
        version.StageDefinitions[0].HasExecutionCondition = true;
        version.StageDefinitions[0].ExecutionCondition = new ExecutionCondition
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration()
        };

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("stage:fulfillment:condition", exception.Path);
    }

    [Fact]
    public void ValidateThrowsWhenEnabledTaskConditionHasNoConfiguration()
    {
        var service = CreateService();
        var version = CreateVersion();
        version.StageDefinitions[0].TaskDefinitions[0].HasExecutionCondition = true;
        version.StageDefinitions[0].TaskDefinitions[0].ExecutionCondition = new ExecutionCondition
        {
            Engine = EngineType.DSL,
            Configuration = null!
        };

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("stage:fulfillment:task:reserve-inventory:condition", exception.Path);
    }

    [Fact]
    public void ValidateAcceptsLiteralConditionsWithoutParsingButterMorphDsl()
    {
        var service = CreateService();
        var version = CreateVersion();
        version.StageDefinitions[0].HasExecutionCondition = true;
        version.StageDefinitions[0].ExecutionCondition = new ExecutionCondition
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration
            {
                Expression = new Expression("false")
            }
        };
        version.StageDefinitions[0].TaskDefinitions[0].HasExecutionCondition = true;
        version.StageDefinitions[0].TaskDefinitions[0].ExecutionCondition = new ExecutionCondition
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration
            {
                Expression = new Expression("true")
            }
        };

        service.Validate(version);
    }

    [Fact]
    public void ValidateThrowsWhenBranchConditionHasNoExpression()
    {
        var service = CreateService();
        var version = CreateVersion();
        version.StageDefinitions[0].BranchRules.Add(new BranchRuleDefinition
        {
            Id = Id.New(),
            FromType = ElementType.Stage,
            FromId = version.StageDefinitions[0].Id,
            NavigateToType = ElementType.Stage,
            NavigateToId = Id.New(),
            Condition = new ExecutionCondition
            {
                Engine = EngineType.DSL,
                Configuration = new DslConditionConfiguration()
            }
        });

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal($"stage:fulfillment:branch:{version.StageDefinitions[0].BranchRules[0].Id}:condition", exception.Path);
    }

    [Fact]
    public void ValidateThrowsWhenTriggerSchemaValidationHasNoDsl()
    {
        var service = CreateService();
        var version = CreateVersion();
        version.TriggerBindings.Add(new TriggerBinding
        {
            Id = Id.New(),
            OrchestrationVersionId = version.Id,
            Key = "sale-created",
            TriggerType = TriggerType.Event,
            IsEnabled = true,
            TriggerChannel = new EventTriggerChannel
            {
                Topic = "events.sales.sale.created",
                Version = new SemanticVersion(1, 0, 0),
                HasSchemaValidation = true
            }
        });

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("trigger:sale-created:event-validation", exception.Path);
    }

    [Fact]
    public void ValidateThrowsWhenEnabledTriggerValidationHasNoDslConfiguration()
    {
        var service = CreateService();
        var version = CreateVersion();
        version.TriggerBindings.Add(new TriggerBinding
        {
            Id = Id.New(),
            OrchestrationVersionId = version.Id,
            Key = "sale-created",
            TriggerType = TriggerType.Event,
            IsEnabled = true,
            TriggerChannel = new EventTriggerChannel
            {
                Topic = "events.sales.sale.created",
                Version = new SemanticVersion(1, 0, 0),
                HasValidation = true,
                Validation = new ValidationDefinition
                {
                    Engine = EngineType.DSL,
                    Configuration = null!
                }
            }
        });

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("trigger:sale-created:event-validation", exception.Path);
        Assert.Contains("must use ButterMorph DSL configuration", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateThrowsWhenCompensationTransformationHasNoDsl()
    {
        var service = CreateService();
        var version = CreateVersion();
        version.StageDefinitions[0].TaskDefinitions[0].CompensationDefinition = new CompensationDefinition
        {
            CompensationTaskKind = TaskKind.Messaging,
            HasTransformation = true,
            Transformation = new TransformationDefinition
            {
                Engine = EngineType.DSL,
                Configuration = new DslTransformationConfiguration()
            },
            Configuration = new MessagingTaskConfiguration
            {
                Topic = "inventories.release",
                Version = new SemanticVersion(1, 0, 0)
            },
            DispatchType = TaskDispatchType.FireAndForget
        };

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("stage:fulfillment:task:reserve-inventory:compensation-transformation", exception.Path);
    }

    [Fact]
    public void ValidateThrowsWhenCompensationConditionHasNoExpression()
    {
        var service = CreateService();
        var version = CreateVersion();
        version.StageDefinitions[0].TaskDefinitions[0].CompensationDefinition = new CompensationDefinition
        {
            CompensationTaskKind = TaskKind.Messaging,
            HasExecutionCondition = true,
            ExecutionCondition = new ExecutionCondition
            {
                Engine = EngineType.DSL,
                Configuration = new DslConditionConfiguration()
            },
            Configuration = new MessagingTaskConfiguration
            {
                Topic = "inventories.release",
                Version = new SemanticVersion(1, 0, 0)
            },
            DispatchType = TaskDispatchType.FireAndForget
        };

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("stage:fulfillment:task:reserve-inventory:compensation-condition", exception.Path);
    }

    [Fact]
    public void ValidateIgnoresNonMessagingCompensationValidationConfiguration()
    {
        var service = CreateService();
        var version = CreateVersion();
        version.StageDefinitions[0].TaskDefinitions[0].CompensationDefinition = new CompensationDefinition
        {
            CompensationTaskKind = TaskKind.Plugin,
            Configuration = new PluginTaskConfiguration { PluginId = Id.New() },
            DispatchType = TaskDispatchType.FireAndForget
        };

        service.Validate(version);
    }

    [Fact]
    public void ValidateThrowsWhenCompensationSchemaValidationHasNoDsl()
    {
        var service = CreateService();
        var version = CreateVersion();
        version.StageDefinitions[0].TaskDefinitions[0].CompensationDefinition = new CompensationDefinition
        {
            CompensationTaskKind = TaskKind.Messaging,
            Configuration = new MessagingTaskConfiguration
            {
                Topic = "inventories.release",
                Version = new SemanticVersion(1, 0, 0),
                HasSchemaValidation = true
            },
            DispatchType = TaskDispatchType.FireAndForget
        };

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("stage:fulfillment:task:reserve-inventory:compensation-request-validation", exception.Path);
    }

    [Fact]
    public void ValidateThrowsWhenCompensationResponseSchemaBindingRequiresValidationButDslIsMissing()
    {
        var service = CreateService();
        var version = CreateVersion();
        version.StageDefinitions[0].TaskDefinitions[0].CompensationDefinition = new CompensationDefinition
        {
            CompensationTaskKind = TaskKind.Messaging,
            Configuration = new MessagingTaskConfiguration
            {
                Topic = "inventories.release",
                Version = new SemanticVersion(1, 0, 0),
                ResponseSchemaBinding = ValidationEnabledBinding()
            },
            DispatchType = TaskDispatchType.FireAndForget
        };

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("stage:fulfillment:task:reserve-inventory:compensation-response-validation", exception.Path);
    }

    [Fact]
    public void ValidateThrowsWhenParsedDslIsNotATransformationDocument()
    {
        var parser = Substitute.For<IDslParser>();
        var analyzer = Substitute.For<ITransformationSemanticAnalyzer>();
        parser.Parse(Arg.Any<IDslDefinition>()).Returns(Substitute.For<IDslDocument>());
        var service = CreateService(parser, analyzer);
        var version = CreateVersion();
        version.StageDefinitions[0].TaskDefinitions[0].HasTransformation = true;
        version.StageDefinitions[0].TaskDefinitions[0].Transformation = DslTransformation("target { Result: source.Value }");

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("stage:fulfillment:task:reserve-inventory:transformation", exception.Path);
        Assert.Contains("not a transformation document", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateThrowsWithDiagnosticsWhenSemanticAnalysisFails()
    {
        var parser = Substitute.For<IDslParser>();
        var analyzer = Substitute.For<ITransformationSemanticAnalyzer>();
        var document = Substitute.For<ITransformationDocument>();
        parser.Parse(Arg.Any<IDslDefinition>()).Returns(document);
        analyzer.Analyze(document).Returns(new SemanticAnalysisResult
        {
            Succeeded = false,
            Diagnostics =
            [
                new DiagnosticEntry
                {
                    Code = "BM001",
                    Message = "Unknown source.",
                    Path = "$.source",
                    Severity = "Error"
                }
            ]
        });
        var service = CreateService(parser, analyzer);
        var version = CreateVersion();
        version.StageDefinitions[0].TaskDefinitions[0].HasTransformation = true;
        version.StageDefinitions[0].TaskDefinitions[0].Transformation = DslTransformation("target { Result: source.Value }");

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("stage:fulfillment:task:reserve-inventory:transformation", exception.Path);
        Assert.Contains("BM001", exception.DiagnosticsJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateThrowsWithEmptyDiagnosticsWhenSemanticAnalysisFailsWithoutDiagnostics()
    {
        var parser = Substitute.For<IDslParser>();
        var analyzer = Substitute.For<ITransformationSemanticAnalyzer>();
        var document = Substitute.For<ITransformationDocument>();
        parser.Parse(Arg.Any<IDslDefinition>()).Returns(document);
        analyzer.Analyze(document).Returns(new SemanticAnalysisResult
        {
            Succeeded = false,
            Diagnostics = null!
        });
        var service = CreateService(parser, analyzer);
        var version = CreateVersion();
        version.StageDefinitions[0].TaskDefinitions[0].HasTransformation = true;
        version.StageDefinitions[0].TaskDefinitions[0].Transformation = DslTransformation("target { Result: source.Value }");

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("stage:fulfillment:task:reserve-inventory:transformation", exception.Path);
        Assert.Equal("[]", exception.DiagnosticsJson);
    }

    [Fact]
    public void ValidateAnalyzesNonLiteralConditionsAsButterMorphTransformations()
    {
        var parser = Substitute.For<IDslParser>();
        var analyzer = Substitute.For<ITransformationSemanticAnalyzer>();
        var document = Substitute.For<ITransformationDocument>();
        IDslDefinition? parsedDefinition = null;
        parser.Parse(Arg.Do<IDslDefinition>(definition => parsedDefinition = definition)).Returns(document);
        analyzer.Analyze(document).Returns(new SemanticAnalysisResult { Succeeded = true });
        var service = CreateService(parser, analyzer);
        var version = CreateVersion();
        version.StageDefinitions[0].HasExecutionCondition = true;
        version.StageDefinitions[0].ExecutionCondition = new ExecutionCondition
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration
            {
                Expression = new Expression("payload.total > 0")
            }
        };

        service.Validate(version);

        Assert.NotNull(parsedDefinition);
        Assert.Contains("target", parsedDefinition!.Content, StringComparison.Ordinal);
        Assert.Contains("Result: payload.total > 0", parsedDefinition.Content, StringComparison.Ordinal);
        analyzer.Received(1).Analyze(document);
    }

    [Fact]
    public void ValidateWrapsParserExceptionsWithDiagnostics()
    {
        var parser = Substitute.For<IDslParser>();
        var analyzer = Substitute.For<ITransformationSemanticAnalyzer>();
        parser.Parse(Arg.Any<IDslDefinition>()).Returns(_ => throw new InvalidOperationException("broken dsl"));
        var service = CreateService(parser, analyzer);
        var version = CreateVersion();
        version.StageDefinitions[0].TaskDefinitions[0].HasTransformation = true;
        version.StageDefinitions[0].TaskDefinitions[0].Transformation = DslTransformation("not valid");

        var exception = Assert.Throws<OrchestrationArtifactDslValidationException>(() => service.Validate(version));

        Assert.Equal("stage:fulfillment:task:reserve-inventory:transformation", exception.Path);
        Assert.Contains("broken dsl", exception.DiagnosticsJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAcceptsValidTriggerTaskAndCompensationDslConfigurations()
    {
        var parser = Substitute.For<IDslParser>();
        var analyzer = Substitute.For<ITransformationSemanticAnalyzer>();
        var document = Substitute.For<ITransformationDocument>();
        parser.Parse(Arg.Any<IDslDefinition>()).Returns(document);
        analyzer.Analyze(document).Returns(new SemanticAnalysisResult { Succeeded = true });
        var service = CreateService(parser, analyzer);
        var version = CreateVersion();
        version.TriggerBindings.Add(new TriggerBinding
        {
            Id = Id.New(),
            OrchestrationVersionId = version.Id,
            Key = "sale-created",
            TriggerType = TriggerType.Event,
            IsEnabled = true,
            TriggerChannel = new EventTriggerChannel
            {
                Topic = "events.sales.sale.created",
                Version = new SemanticVersion(1, 0, 0),
                HasValidation = true,
                Validation = DslValidation("target { Result: source.saleId != null }")
            }
        });
        var task = version.StageDefinitions[0].TaskDefinitions[0];
        task.HasTransformation = true;
        task.Transformation = DslTransformation("target { saleId: source.saleId }");
        var messaging = (MessagingTaskConfiguration)task.Configuration;
        messaging.HasRequestValidation = true;
        messaging.RequestValidation = DslValidation("target { Result: source.saleId != null }");
        messaging.HasResponseValidation = true;
        messaging.ResponseValidation = DslValidation("target { Result: source.reserved == true }");
        task.CompensationDefinition = new CompensationDefinition
        {
            CompensationTaskKind = TaskKind.Messaging,
            DispatchType = TaskDispatchType.FireAndForget,
            HasTransformation = true,
            Transformation = DslTransformation("target { saleId: source.saleId }"),
            HasExecutionCondition = true,
            ExecutionCondition = new ExecutionCondition
            {
                Engine = EngineType.DSL,
                Configuration = new DslConditionConfiguration
                {
                    Expression = new Expression("payload.reserveFailed == true")
                }
            },
            Configuration = new MessagingTaskConfiguration
            {
                Topic = "inventories.release",
                Version = new SemanticVersion(1, 0, 0),
                HasRequestValidation = true,
                RequestValidation = DslValidation("target { Result: source.saleId != null }"),
                HasResponseValidation = true,
                ResponseValidation = DslValidation("target { Result: source.released == true }")
            }
        };

        service.Validate(version);

        Assert.True(parser.ReceivedCalls().Count() >= 7);
        Assert.True(analyzer.ReceivedCalls().Count() >= 7);
    }

    private static IOrchestrationArtifactDslValidationService CreateService()
    {
        var services = new ServiceCollection();
        DesignApplicationServices.AddOrchestratorDesignApplication(services);
        return services.BuildServiceProvider().GetRequiredService<IOrchestrationArtifactDslValidationService>();
    }

    private static IOrchestrationArtifactDslValidationService CreateService(
        IDslParser parser,
        ITransformationSemanticAnalyzer analyzer)
        => new OrchestrationArtifactDslValidationService(parser, analyzer);

    private static TransformationDefinition DslTransformation(string dsl)
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration { Dsl = dsl }
        };

    private static ValidationDefinition DslValidation(string dsl)
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslValidationConfiguration { Dsl = dsl }
        };

    private static SchemaBinding ValidationEnabledBinding()
        => new()
        {
            Id = Id.New(),
            ElementType = ElementType.Task,
            ElementId = Id.New(),
            ContractId = Id.New(),
            ContractKey = "inventories.reserve.reply",
            ContractVersion = new SemanticVersion(1, 0, 0),
            IsValidationEnabled = true
        };

    private static OrchestrationVersion CreateVersion()
    {
        var versionId = Id.New();
        var stageId = Id.New();
        var taskId = Id.New();

        return new OrchestrationVersion
        {
            Id = versionId,
            OrchestrationDefinitionId = Id.New(),
            Version = new SemanticVersion(1, 0, 0),
            VersionLabel = "1.0.0",
            Checksum = new Checksum("checksum"),
            Status = OrchestrationVersionStatus.Approved,
            CreatedBy = "tests",
            CreatedOnUtc = DateTime.UtcNow,
            StageDefinitions =
            [
                new StageDefinition
                {
                    Id = stageId,
                    OrchestrationVersionId = versionId,
                    Key = "fulfillment",
                    Name = "Fulfillment",
                    Order = 1,
                    TaskDefinitions =
                    [
                        new TaskDefinition
                        {
                            Id = taskId,
                            StageDefinitionId = stageId,
                            Key = "reserve-inventory",
                            Name = "Reserve inventory",
                            Order = 1,
                            Kind = TaskKind.Messaging,
                            Configuration = new MessagingTaskConfiguration
                            {
                                Topic = "inventories.reserve",
                                Version = new SemanticVersion(1, 0, 0)
                            },
                            IsEnabled = true
                        }
                    ]
                }
            ]
        };
    }
}
