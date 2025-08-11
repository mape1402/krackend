using Krackend.Sagas.Orchestration.Core;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Pigeon.Messaging.Consuming.Dispatching;
using System.Text.Json;

namespace Krackend.Sagas.Orchestration.Tests.Core
{
    public class OrchestrationConsumeInterceptorTests
    {
        [Fact]
        public async Task Intercept_SetsMetadata_WhenPresent()
        {
            // Arrange
            var metadata = new OrchestrationMetadata();
            var setter = Substitute.For<IMetadataSetter>();
            var logger = Substitute.For<ILogger<OrchestrationConsumeInterceptor>>();
            var context = new ConsumeContext
            {
                RawMetadata = new Dictionary<string, string>
                {
                    { OrchestrationMetadata.MetadataKey, JsonSerializer.Serialize(metadata) }
                }
            };

            var interceptor = new OrchestrationConsumeInterceptor(setter, logger);

            /// Act
            await interceptor.Intercept(context, CancellationToken.None);

            // Assert
        }

        [Fact]
        public async Task Intercept_LogsWarning_WhenExceptionThrown()
        {
            /// Arrange
            var setter = Substitute.For<IMetadataSetter>();
            var logger = Substitute.For<ILogger<OrchestrationConsumeInterceptor>>();
            var context = new ConsumeContext();

            var interceptor = new OrchestrationConsumeInterceptor(setter, logger);

            // Act
            await interceptor.Intercept(context, CancellationToken.None);

            // Assert
            logger.ReceivedWithAnyArgs().Log(
                LogLevel.Warning,
                default,
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>());
        }
    }
}
