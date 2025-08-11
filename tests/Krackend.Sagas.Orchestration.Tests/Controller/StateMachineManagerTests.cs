using Krackend.Sagas.Orchestration.Controller.Configuration.Builders;
using Krackend.Sagas.Orchestration.Controller.Configuration.Metadata;
using Krackend.Sagas.Orchestration.Controller.Dispatching;
using Krackend.Sagas.Orchestration.Controller.Tracking;
using NSubstitute;
using Pigeon.Messaging.Consuming.Configuration;

namespace Krackend.Sagas.Orchestration.Tests.Controller
{
    public class StateMachineManagerTests
    {
        
        public async Task CreateNewStateMachine_ShouldInitializeCorrectly()
        {
            // Arrange
            var roadmapManager = new RoadmapManager();
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();

            var orchestratorBuilder = new OrchestratorBuilder(consumingConfigurator, roadmapManager);
            orchestratorBuilder.Add(new DemoOrchestration());

            var roadmap = roadmapManager.GetRoadmap("test-topic");

            var sagaId = "test-saga-id";
            var stateMachineManager = new StateMachineManager();
            var stateMchine = stateMachineManager.CreateNewStateMachine(roadmap, sagaId);

            var state = new SagaExecutionState(roadmap, stateMchine)
            {
                SagaId = sagaId
            };

            var stateManager = Substitute.For<IStateManager>();
            stateManager.CreateState(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(state);

            var dispatcher = new Dispatcher(stateManager);

            // Act
            await dispatcher.StartAsync("test-topic", "1.0.0", "payload", CancellationToken.None);
        }
    }

    public class DemoOrchestration : IOrchestration
    {
        public void Configure(IRoadmapBuilder builder)
        {
            builder
                .Set("test", "test")
                .SubscribeTo("test-topic", "1.0.0")
                .Next(stage =>
                {
                    stage
                        .Set("stage1", "stage1")
                        .StepForward(config =>
                        {
                            config
                                .ResolveTo("stage1");
                        });
                })
                .Next(stage =>
                {
                    stage
                        .Set("stage2", "stage2")
                        .StepForward(config =>
                        {
                            config
                                .ResolveTo("stage2");
                        });
                });
        }
    }
}
