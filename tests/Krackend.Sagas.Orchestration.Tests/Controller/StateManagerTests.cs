using Krackend.Sagas.Orchestration.Controller.Configuration.Metadata;
using Krackend.Sagas.Orchestration.Controller.Dispatching;
using Krackend.Sagas.Orchestration.Controller.Tracking;
using Krackend.Sagas.Orchestration.Core;
using NSubstitute;

namespace Krackend.Sagas.Orchestration.Tests.Controller
{
    public class StateManagerTests
    {
        [Fact]
        public void Constructor_NullArguments_ThrowsArgumentNullException()
        {
            var ctx = Substitute.For<IOrchestrationContext>();
            var roadmap = Substitute.For<IRoadmapManager>();
            var smm = Substitute.For<IStateMachineManager>();
            var idGen = Substitute.For<ISagaIdGenerator>();
            var sp = Substitute.For<IServiceProvider>();
            Assert.Throws<ArgumentNullException>(() => new StateManager(null, roadmap, smm, idGen, sp));
            Assert.Throws<ArgumentNullException>(() => new StateManager(ctx, null, smm, idGen, sp));
            Assert.Throws<ArgumentNullException>(() => new StateManager(ctx, roadmap, null, idGen, sp));
            Assert.Throws<ArgumentNullException>(() => new StateManager(ctx, roadmap, smm, null, sp));
            Assert.Throws<ArgumentNullException>(() => new StateManager(ctx, roadmap, smm, idGen, null));
        }

        [Fact]
        public async Task CreateState_RoadmapNull_ThrowsInvalidOperationException()
        {
            var ctx = Substitute.For<IOrchestrationContext>();
            var roadmapManager = Substitute.For<IRoadmapManager>();
            roadmapManager.GetRoadmap(Arg.Any<string>()).Returns((Roadmap)null);
            var smm = Substitute.For<IStateMachineManager>();
            var idGen = Substitute.For<ISagaIdGenerator>();
            var sp = Substitute.For<IServiceProvider>();
            var manager = new StateManager(ctx, roadmapManager, smm, idGen, sp);
            await Assert.ThrowsAsync<InvalidOperationException>(() => manager.CreateState("topic"));
        }

        [Fact]
        public async Task GetCurrentState_NoMetadata_ThrowsInvalidOperationException()
        {
            var ctx = Substitute.For<IOrchestrationContext>();
            ctx.HasMetadata().Returns(false);
            var roadmap = Substitute.For<IRoadmapManager>();
            var smm = Substitute.For<IStateMachineManager>();
            var idGen = Substitute.For<ISagaIdGenerator>();
            var sp = Substitute.For<IServiceProvider>();
            var manager = new StateManager(ctx, roadmap, smm, idGen, sp);
            await Assert.ThrowsAsync<InvalidOperationException>(() => manager.GetCurrentState());
        }
    }
}
