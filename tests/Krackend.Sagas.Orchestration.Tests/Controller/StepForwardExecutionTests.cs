using Krackend.Sagas.Orchestration.Controller.Configuration.Metadata;
using Krackend.Sagas.Orchestration.Controller.Dispatching;
using Krackend.Sagas.Orchestration.Controller.Tracking.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;

namespace Krackend.Sagas.Orchestration.Tests.Controller
{
    public class StepForwardExecutionTests
    {
        [Fact]
        public void Constructor_NullOrWhitespaceTopic_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new StepForwardExecution(null, SemanticVersion.Default));
            Assert.Throws<ArgumentNullException>(() => new StepForwardExecution("", SemanticVersion.Default));
        }

        [Fact]
        public async Task ProcessAsync_PublishesMessageAndLogsInformation()
        {
            var topic = "test-topic";
            var version = SemanticVersion.Default;
            var payload = "payload";
            var sagaId = "saga-1";

            var producer = Substitute.For<IProducer>();
            producer.PublishAsync(payload, topic, version, Arg.Any<CancellationToken>()).Returns(ValueTask.CompletedTask);

            var logger = Substitute.For<ILogger<StepForwardExecution>>();
            var services = new ServiceCollection()
                .AddSingleton(producer)
                .AddSingleton(logger)
                .BuildServiceProvider();

            var roadmap = new Roadmap
            {
                Stages = new List<StageMetadata>
                {
                   new StageMetadata
                   {
                       Id = "stage-1",
                       Name = "Stage 1",
                       StepForward = new StepMetadata
                       {
                           ResolveTo = topic,
                           Version = version
                       }
                   }
                }
            };

            var state = new SagaExecutionState(roadmap, new StateMachine())
            {
               SagaId = sagaId,
               Services = services
            };

            state.SetPayload(payload);

            var step = new StepForwardExecution(topic, version);
            await step.ProcessAsync(state);
            await producer.Received(1).PublishAsync(payload, topic, version, Arg.Any<CancellationToken>());
        }
    }
}
