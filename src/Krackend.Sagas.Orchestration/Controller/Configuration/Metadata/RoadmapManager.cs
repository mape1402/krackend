namespace Krackend.Sagas.Orchestration.Controller.Configuration.Metadata
{
    using Krackend.Sagas.Orchestration.Controller.Shared;

    /// <summary>
    /// Provides an implementation of <see cref="IRoadmapManager"/> for managing orchestration roadmaps.
    /// Thread-safe implementation using locking.
    /// </summary>
    internal class RoadmapManager : IRoadmapManager
    {
        private readonly IList<Roadmap> _roadmaps = new List<Roadmap>();
        private readonly object _sync = new object();

        /// <inheritdoc />
        public Roadmap GetRoadmap(string topic)
        {
            lock (_sync)
            {
                return _roadmaps.FirstOrDefault(x => x.EventTopic == topic);
            }
        }

        /// <inheritdoc />
        public IRoadmapManager AddRoadmap(Roadmap roadmap)
        {
            lock (_sync)
            {
                if (_roadmaps.Any(x => x.OrchestrationKey == roadmap.OrchestrationKey || x.EventTopic == roadmap.EventTopic))
                    throw new InvalidOperationException("Current orchestration-roadmap already registered.");

                _roadmaps.Add(roadmap);
                return this;
            }
        }

        /// <inheritdoc />
        public IRoadmapManager RemoveRoadmap(string orchestrationKey)
        {
            lock (_sync)
            {
                var roadmap = InternalGet(orchestrationKey);

                if (roadmap.IsActive)
                    throw new InvalidOperationException($"Orchestration-roadmap cannot be removed. Inactive this roadmap before to remove it.");

                _roadmaps.Remove(roadmap);
                return this;
            }
        }

        /// <inheritdoc />
        public IRoadmapManager InactiveRoadmap(string orchestrationKey)
        {
            lock (_sync)
            {
                var roadmap = InternalGet(orchestrationKey);
                roadmap.Inactive();
                return this;
            }
        }

        /// <summary>
        /// Marks the roadmap with the specified orchestration key as active.
        /// </summary>
        public IRoadmapManager ActiveRoadmap(string orchestrationKey)
        {
            lock (_sync)
            {
                var roadmap = InternalGet(orchestrationKey);
                roadmap.Active();
                return this;
            }
        }

        /// <inheritdoc />
        public IRoadmapManager UpdateRoadmap(string orchestratorKey, Roadmap roadmap)
        {
            lock (_sync)
            {
                RemoveRoadmap(orchestratorKey);
                AddRoadmap(roadmap);
                return this;
            }
        }

        /// <inheritdoc />
        public IStep GetStepExecution(string orchestrationKey, string stageId, SagaState direction)
        {
            lock (_sync)
            {
                var roadmap = InternalGet(orchestrationKey);
                var stage = roadmap.Stages.FirstOrDefault(x => x.Id == stageId);

                if (stage == null)
                    throw new InvalidOperationException($"Stage with id '{stageId}' into orchestration with key '{orchestrationKey}' doesn't exist.");

                return direction == SagaState.Forward
                    ? stage.StepForward.StepExecution
                    : stage.Compensation.StepExecution;
            }
        }

        /// <summary>
        /// Gets the roadmap with the specified orchestration key or throws if not found.
        /// </summary>
        private Roadmap InternalGet(string orchestrationKey)
        {
            var roadmap = _roadmaps.FirstOrDefault(x => x.OrchestrationKey == orchestrationKey);

            if (roadmap == null)
                throw new InvalidOperationException($"Orchestration-roadmap with key '{orchestrationKey}' doesn't exist.");

            return roadmap;
        }
    }
}
