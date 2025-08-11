namespace Krackend.Sagas.Orchestration.Controller.Configuration.Metadata
{
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Represents a roadmap for saga orchestration, containing configuration and stages.
    /// </summary>
    public class Roadmap
    {
        private Dictionary<string, StageMetadata> _stages;
        private Dictionary<string, int> _indexes;
        private Dictionary<int, string> _reverseIndexes;
        private int _maxIndex;

        private bool _isActive;

        /// <summary>
        /// Gets or sets the orchestration key.
        /// </summary>
        public string OrchestrationKey { get; init; }

        /// <summary>
        /// Gets or sets the orchestration name.
        /// </summary>
        public string OrchestrationName { get; init; }

        /// <summary>
        /// Gets or sets the description of the roadmap.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        /// Gets or sets the event topic.
        /// </summary>
        public string EventTopic { get; init; }

        /// <summary>
        /// Gets or sets the event topic version.
        /// </summary>
        public SemanticVersion EventTopicVersion { get; init; }

        /// <summary>
        /// Gets or sets the orchestration topic.
        /// </summary>
        public string OrchestrationTopic { get; init; }

        /// <summary>
        /// Gets or sets the orchestration topic version.
        /// </summary>
        public SemanticVersion OrchestrationTopicVersion { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the roadmap is active.
        /// </summary>
        public bool IsActive 
        { 
            get => _isActive; 
            init => _isActive = value; 
        }

        /// <summary>
        /// Gets or sets the collection of stages in the roadmap.
        /// </summary>
        public IEnumerable<StageMetadata> Stages 
        { 
            get => _stages.Values;
            init => InitStages(value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Roadmap"/> class.
        /// </summary>
        public Roadmap()
        {
            _stages = new Dictionary<string, StageMetadata>();
            _indexes = new Dictionary<string, int>();
            _reverseIndexes = new Dictionary<int, string>();
            _maxIndex = -1;
            _isActive = false;
        }

        /// <summary>
        /// Gets the stage metadata by identifier.
        /// </summary>
        /// <param name="id">The stage identifier.</param>
        /// <returns>The <see cref="StageMetadata"/> instance if found; otherwise, null.</returns>
        public StageMetadata GetStageById(string id)
        {
            if(_stages.TryGetValue(id, out var stage))
                return stage;

            return default;
        }

        /// <summary>
        /// Gets the first stage in the roadmap.
        /// </summary>
        /// <returns>The first <see cref="StageMetadata"/> instance or null if no stages exist.</returns>
        public StageMetadata GetFirstStage() 
            => Stages.FirstOrDefault();

        /// <summary>
        /// Gets the last stage in the roadmap.
        /// </summary>
        /// <returns>The last <see cref="StageMetadata"/> instance or null if no stages exist.</returns>
        public StageMetadata GetLastStage() 
            => Stages.LastOrDefault();

        /// <summary>
        /// Moves to the next stage in the roadmap.
        /// </summary>
        /// <param name="stageId">The current stage identifier.</param>
        /// <returns>The next <see cref="StageMetadata"/> instance or null if there is no next stage.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the current stage ID is not found.</exception>
        public StageMetadata NextStage(string stageId)
        {
            if(!_indexes.TryGetValue(stageId, out var currentIndex))
                throw new InvalidOperationException($"Stage index with ID '{stageId}' not found.");
        
            currentIndex++;

            if(currentIndex > _maxIndex)
                return default;

            if (!_reverseIndexes.TryGetValue(currentIndex, out var nextStageId))
                return default;

            return GetStageById(nextStageId);
        }

        /// <summary>
        /// Moves to the previous stage in the roadmap.
        /// </summary>
        /// <param name="stageId">The current stage identifier.</param>
        /// <returns>The previous <see cref="StageMetadata"/> instance or null if there is no previous stage.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the current stage ID is not found.</exception>
        public StageMetadata PreviousStage(string stageId)
        {
            if (!_indexes.TryGetValue(stageId, out var currentIndex))
                throw new InvalidOperationException($"Stage index with ID '{stageId}' not found.");

            currentIndex--;

            if (currentIndex < 0)
                return default;

            if (!_reverseIndexes.TryGetValue(currentIndex, out var nextStageId))
                return default;

            return GetStageById(nextStageId);
        }

        /// <summary>
        /// Activates the roadmap, setting it as active.
        /// </summary>
        public void Active()
            => _isActive = true;

        /// <summary>
        /// Deactivates the roadmap, setting it as inactive.
        /// </summary>
        public void Inactive() 
            => _isActive = false;

        private void InitStages(IEnumerable<StageMetadata> stages)
        {
            if (stages == null || !stages.Any())
                throw new ArgumentException("Stages cannot be null or empty.", nameof(stages));

            _stages = stages.ToDictionary(s => s.Id, s => s);

            _indexes = stages.Select((s, i) => new { s.Id, Index = i })
                            .ToDictionary(x => x.Id, x => x.Index);

            _reverseIndexes = stages.Select((s, i) => new { s.Id, Index = i })
                                  .ToDictionary(x => x.Index, x => x.Id);

            _maxIndex = stages.Count() - 1;
        }
    }
}
