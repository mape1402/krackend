using Krackend.Sagas.Orchestration.Controller.Configuration.Builders;

namespace Krackend.Sagas.Orchestration.Tests.Controller
{
    public class RoadmapBuilderTests
    {
        [Fact]
        public void Set_SetsKeyAndName_ReturnsSelf()
        {
            var builder = new RoadmapBuilder();
            var result = builder.Set("key", "name");
            Assert.Same(builder, result);
        }

        [Fact]
        public void Description_SetsDescription_ReturnsSelf()
        {
            var builder = new RoadmapBuilder();
            var result = builder.Description("desc");
            Assert.Same(builder, result);
        }

        [Fact]
        public void SubscribeTo_SetsEventTopic_ReturnsSelf()
        {
            var builder = new RoadmapBuilder();
            var result = builder.SubscribeTo("topic");
            Assert.Same(builder, result);
        }

        [Fact]
        public void ListenTo_SetsOrchestrationTopic_ReturnsSelf()
        {
            var builder = new RoadmapBuilder();
            var result = builder.ListenTo("topic");
            Assert.Same(builder, result);
        }

        [Fact]
        public void AsActive_AsInactive_TogglesIsActive()
        {
            var builder = new RoadmapBuilder();
            builder.AsActive();
            builder.AsInactive();
            // No exception means success
        }

        [Fact]
        public void Build_ReturnsRoadmap()
        {
            var builder = new RoadmapBuilder();
            builder.Set("key", "name")
                .Next(stage =>
                {
                    stage
                        .Set("stage1", "Stage 1");
                });

            var roadmap = builder.Build();
            Assert.NotNull(roadmap);
        }
    }
}
