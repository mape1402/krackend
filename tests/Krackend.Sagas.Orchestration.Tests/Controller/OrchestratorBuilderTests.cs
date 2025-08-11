using Krackend.Sagas.Orchestration.Controller.Configuration.Builders;
using Krackend.Sagas.Orchestration.Controller.Configuration.Metadata;
using NSubstitute;
using Pigeon.Messaging.Consuming.Configuration;

namespace Krackend.Sagas.Orchestration.Tests.Controller
{
    public class OrchestratorBuilderTests
    {
        [Fact]
        public void Constructor_NullArguments_ThrowsArgumentNullException()
        {
            var roadmapManager = Substitute.For<IRoadmapManager>();
            Assert.Throws<ArgumentNullException>(() => new OrchestratorBuilder(null, roadmapManager));
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            Assert.Throws<ArgumentNullException>(() => new OrchestratorBuilder(consumingConfigurator, null));
        }

        [Fact]
        public void Add_NullOrchestration_ThrowsArgumentNullException()
        {
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            var roadmapManager = Substitute.For<IRoadmapManager>();
            var builder = new OrchestratorBuilder(consumingConfigurator, roadmapManager);
            Assert.Throws<ArgumentNullException>(() => builder.Add(null));
        }

        [Fact]
        public void Start_CallsAddForEachOrchestration()
        {
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            var roadmapManager = Substitute.For<IRoadmapManager>();
            var builder = new OrchestratorBuilder(consumingConfigurator, roadmapManager);
            var orchestrations = new List<IOrchestration> { new TestOrchestration() };
            builder.Start(orchestrations);
            // No exception means success
        }
    }

    public class TestOrchestration : IOrchestration
    {
        public void Configure(IRoadmapBuilder builder)
        {
            builder
                .Set("Test", "Test")
                .Next(stage =>
                {
                    stage
                        .Set("Stage1", "Stage1");
                });
        }
    }
}
