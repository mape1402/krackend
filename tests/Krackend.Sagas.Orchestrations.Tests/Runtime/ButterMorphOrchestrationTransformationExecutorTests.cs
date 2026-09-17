namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using global::ButterMorph.Abstractions;
using global::ButterMorph.Core;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.ButterMorph;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Transformations;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using RuntimeButterMorphServices = Krackend.Sagas.Orchestrations.Runtime.ButterMorph.DependencyInjection.ServiceCollectionExtensions;

public sealed class ButterMorphOrchestrationTransformationExecutorTests
{
    [Fact]
    public async Task TransformAsyncFailsWhenTransformationDslIsMissing()
    {
        var result = await CreateExecutor().TransformAsync(new OrchestrationTransformationRequest
        {
            Task = CreateTask(string.Empty),
            PayloadContext = EmptyPayloadContext()
        });

        Assert.False(result.Succeeded);
        Assert.Equal("TransformationDslMissing", result.ErrorCode);
    }

    [Fact]
    public async Task TransformAsyncMapsButterMorphFailedResultToRuntimeFailure()
    {
        var engine = Substitute.For<IButterMorphEngine>();
        var parser = Substitute.For<IDslParser>();
        var sourceGraphBuilder = Substitute.For<IButterMorphSourceGraphBuilder>();
        parser.Parse(Arg.Any<IDslDefinition>()).Returns(new DslDocument());
        sourceGraphBuilder.Build(Arg.Any<OrchestrationPayloadContext>())
            .Returns(new Dictionary<string, IStructureGraph>(StringComparer.Ordinal));
        engine.Transform(Arg.Any<TransformationRequest>())
            .Returns(new TransformationResult
            {
                Succeeded = false,
                Diagnostics =
                [
                    new DiagnosticEntry
                    {
                        Code = "mapping",
                        Message = "Unable to map sale id",
                        Path = "$.saleId",
                        Severity = "Error"
                    }
                ]
            });
        var executor = new ButterMorphOrchestrationTransformationExecutor(
            engine,
            parser,
            new ButterMorphDiagnosticMetadataMapper(),
            sourceGraphBuilder);

        var result = await executor.TransformAsync(new OrchestrationTransformationRequest
        {
            Task = CreateTask("target { SaleId: $trigger.SaleId }"),
            PayloadContext = EmptyPayloadContext()
        });

        Assert.False(result.Succeeded);
        Assert.Equal("TransformationFailed", result.ErrorCode);
        Assert.True(result.Diagnostics.ContainsKey("diagnostics"));
    }

    [Fact]
    public async Task TransformAsyncMapsButterMorphExceptionsToExecutionFailure()
    {
        var engine = Substitute.For<IButterMorphEngine>();
        var parser = Substitute.For<IDslParser>();
        var sourceGraphBuilder = Substitute.For<IButterMorphSourceGraphBuilder>();
        parser.Parse(Arg.Any<IDslDefinition>()).Returns(_ => throw new InvalidOperationException("invalid transform dsl"));
        var executor = new ButterMorphOrchestrationTransformationExecutor(
            engine,
            parser,
            new ButterMorphDiagnosticMetadataMapper(),
            sourceGraphBuilder);

        var result = await executor.TransformAsync(new OrchestrationTransformationRequest
        {
            Task = CreateTask("bad dsl"),
            PayloadContext = EmptyPayloadContext()
        });

        Assert.False(result.Succeeded);
        Assert.Equal("TransformationExecutionFailed", result.ErrorCode);
        Assert.True(result.Diagnostics.ContainsKey("exceptionType"));
    }

    [Fact]
    public async Task TransformAsyncCanReadAccumulatedTaskResponsesWithoutWrappingBusinessPayload()
    {
        var executor = CreateExecutor();

        var result = await executor.TransformAsync(new OrchestrationTransformationRequest
        {
            Task = CreateTask(
                """
                target {
                  SaleId: $trigger.SaleId
                  CustomerId: $trigger.CustomerId
                  Total: $trigger.Total
                  ReservationId: $responses.inventory_reservation.inventories_reserve.ReservationId
                  PaymentId: $responses.payment_capture.payments_capture.PaymentId
                }
                """),
            PayloadContext = new OrchestrationPayloadContext
            {
                ContextPayload = JsonNode.Parse(
                    """
                    {
                      "trigger": {
                        "payload": {
                          "SaleId": "sale-1",
                          "CustomerId": "customer-1",
                          "Total": 42.5
                        }
                      },
                      "stages": {
                        "inventory-reservation": {
                          "tasks": {
                            "inventories.reserve": {
                              "response": {
                                "ReservationId": "reservation-1"
                              }
                            }
                          }
                        },
                        "payment-capture": {
                          "tasks": {
                            "payments.capture": {
                              "response": {
                                "PaymentId": "payment-1"
                              }
                            }
                          }
                        }
                      },
                      "variables": {}
                    }
                    """)!,
                TriggerPayload = JsonNode.Parse(
                    """
                    {
                      "SaleId": "sale-1",
                      "CustomerId": "customer-1",
                      "Total": 42.5
                    }
                    """)!,
                StageKey = "sale-completion",
                TaskKey = "sales.complete"
            }
        });

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("sale-1", result.Payload!["SaleId"]?.GetValue<string>());
        Assert.Equal("customer-1", result.Payload!["CustomerId"]?.GetValue<string>());
        Assert.Equal(42.5m, result.Payload!["Total"]?.GetValue<decimal>());
        Assert.Equal("reservation-1", result.Payload!["ReservationId"]?.GetValue<string>());
        Assert.Equal("payment-1", result.Payload!["PaymentId"]?.GetValue<string>());
        Assert.Null(result.Payload!["Succeeded"]);
        Assert.Null(result.Payload!["Metadata"]);
        Assert.Null(result.Payload!["Error"]);
    }

    [Fact]
    public async Task TransformAsyncCanReadVariablesAndSanitizedStageAliases()
    {
        var executor = CreateExecutor();

        var result = await executor.TransformAsync(new OrchestrationTransformationRequest
        {
            Task = CreateTask(
                """
                target {
                  Region: $variables.Region
                  ReservationId: $stages.inventory_reservation.tasks.inventories_reserve.response.ReservationId
                }
                """),
            PayloadContext = new OrchestrationPayloadContext
            {
                ContextPayload = JsonNode.Parse(
                    """
                    {
                      "trigger": {
                        "payload": {
                          "SaleId": "sale-1"
                        }
                      },
                      "stages": {
                        "inventory-reservation": {
                          "tasks": {
                            "inventories.reserve": {
                              "response": {
                                "ReservationId": "reservation-1"
                              }
                            }
                          }
                        }
                      },
                      "variables": {
                        "Region": "north"
                      }
                    }
                    """)!,
                TriggerPayload = JsonNode.Parse(
                    """
                    {
                      "SaleId": "sale-1"
                    }
                    """)!,
                StageKey = "sale-completion",
                TaskKey = "sales.complete"
            }
        });

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("north", result.Payload!["Region"]?.GetValue<string>());
        Assert.Equal("reservation-1", result.Payload!["ReservationId"]?.GetValue<string>());
    }

    private static IOrchestrationTransformationExecutor CreateExecutor()
    {
        var services = new ServiceCollection();
        RuntimeButterMorphServices.AddKrackendOrchestrationsRuntimeButterMorph(services);
        return services
            .BuildServiceProvider()
            .GetRequiredService<IOrchestrationTransformationExecutor>();
    }

    private static TaskArtifact CreateTask(string transformationDsl)
        => new(
            Id.New(),
            "sales.complete",
            "Complete sale",
            1,
            string.Empty,
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            null,
            null,
            new TransformationArtifact(EngineType.DSL, new DslTransformationConfigurationArtifact
            {
                Dsl = transformationDsl
            })
            {
                IsEnabled = true
            },
            new MessagingTaskConfigurationArtifact("commands.sales.sale.complete", new SemanticVersion(1, 2, 0), null),
            null,
            null,
            OnErrorPolicy.Stop,
            null,
            TaskDispatchType.FireAndWait,
            true);

    private static OrchestrationPayloadContext EmptyPayloadContext()
        => new()
        {
            ContextPayload = JsonNode.Parse("""{"trigger":{"payload":{}},"stages":{},"variables":{}}""")!,
            TriggerPayload = JsonNode.Parse("{}")!,
            StageKey = "inventory-reservation",
            TaskKey = "inventories.reserve"
        };
}
