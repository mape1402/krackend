using Krackend.Sagas.Orchestration.Core;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Pigeon.Messaging.Producing;

namespace Krackend.Sagas.Orchestration.Tests.Core
{
    public class OrchestrationPublishInterceptorTests
    {
        [Fact]
        public async Task Intercept_AddsMetadata_WhenPresent()
        {
            // Arrange
            var metadata = new OrchestrationMetadata();
            var context = Substitute.For<IOrchestrationContext>();
            var logger = Substitute.For<ILogger<OrchestrationPublishInterceptor>>();
            context.HasMetadata().Returns(true);
            context.GetMetadata().Returns(metadata);
            var publishContext = Substitute.For<PublishContext>();
            var interceptor = new OrchestrationPublishInterceptor(context, logger);

            // Act
            await interceptor.Intercept(publishContext, CancellationToken.None);

            // Assert
        }

        [Fact]
        public async Task Intercept_LogsWarning_WhenNoMetadata()
        {
            // Arrange
            var context = Substitute.For<IOrchestrationContext>();
            var logger = Substitute.For<ILogger<OrchestrationPublishInterceptor>>();
            context.HasMetadata().Returns(false);
            var publishContext = Substitute.For<PublishContext>();
            var interceptor = new OrchestrationPublishInterceptor(context, logger);

            /// Act
            await interceptor.Intercept(publishContext, CancellationToken.None);

            // Assert
            logger.ReceivedWithAnyArgs().Log(
                LogLevel.Warning,
                default,
                Arg.Any<object>(),
                null,
                Arg.Any<Func<object, Exception, string>>());
        }
    }
}
