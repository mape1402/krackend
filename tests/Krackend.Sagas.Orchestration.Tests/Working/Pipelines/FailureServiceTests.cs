using Krackend.Sagas.Orchestration.Core;
using Krackend.Sagas.Orchestration.Worker;
using Krackend.Sagas.Orchestration.Worker.Pipelines;
using NSubstitute;
using Pigeon.Messaging.Producing;
using Spider.Pipelines.Core;

namespace Krackend.Sagas.Orchestration.Tests.Working.Pipelines
{
    public class FailureServiceTests
    {
        [Fact]
        public async Task ForwardError_SetsFailureAndPublishesPayload()
        {
            // Arrange
            var orchestrationContext = Substitute.For<IOrchestrationContext>();
            var metadataSetter = Substitute.For<IMetadataSetter>();
            var exceptionEvaluator = Substitute.For<IExceptionEvaluator>();
            var producer = Substitute.For<IProducer>();
            var metadata = new OrchestrationMetadata { OrchestrationTopic = "reply-topic" };
            var errorMetadata = new ErrorMetadata { Code = 1, Message = "err", StackTrace = "stack" };
            orchestrationContext.HasMetadata().Returns(true);
            orchestrationContext.GetMetadata().Returns(metadata);
            exceptionEvaluator.ExtractMetadata(Arg.Any<System.Exception>()).Returns(errorMetadata);
            var context = Substitute.For<IReadOnlyContext<string>>();
            context.Request.Returns("payload");
            context.Exception.Returns(new System.Exception("fail"));
            context.CancellationToken.Returns(CancellationToken.None);
            var service = new FailureService(orchestrationContext, metadataSetter, exceptionEvaluator, producer);

            // Act
            await service.ForwardError(context, null, null);

            // Assert
            metadataSetter.Received(1).SetAsFailure(errorMetadata);
            await producer.Received(1).PublishAsync("payload", "reply-topic", CancellationToken.None);
        }
    }
}
