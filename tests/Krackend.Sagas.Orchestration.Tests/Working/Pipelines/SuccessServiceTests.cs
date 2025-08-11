using Krackend.Sagas.Orchestration.Core;
using Krackend.Sagas.Orchestration.Worker.Pipelines;
using NSubstitute;
using Pigeon.Messaging.Producing;
using Spider.Pipelines.Core;

namespace Krackend.Sagas.Orchestration.Tests.Working.Pipelines
{
    public class SuccessServiceTests
    {
        [Fact]
        public async Task Forward_SetsSuccessAndPublishesPayload()
        {
            // Arrange
            var orchestrationContext = Substitute.For<IOrchestrationContext>();
            var metadataSetter = Substitute.For<IMetadataSetter>();
            var producer = Substitute.For<IProducer>();
            var metadata = new OrchestrationMetadata { OrchestrationTopic = "reply-topic" };
            orchestrationContext.HasMetadata().Returns(true);
            orchestrationContext.GetMetadata().Returns(metadata);
            var context = Substitute.For<IReadOnlyContext<string>>();
            context.Request.Returns("payload");
            context.CancellationToken.Returns(CancellationToken.None);
            var service = new SuccessService(orchestrationContext, metadataSetter, producer);

            // Act
            await service.Forward(context, null, null);

            // Assert
            metadataSetter.Received(1).SetAsSuccess();
            await producer.Received(1).PublishAsync("payload", "reply-topic", CancellationToken.None);
        }
    }
}
