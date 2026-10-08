namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

public sealed class OrchestrationOriginMetadataTests
{
    public static IEnumerable<object[]> PopulatedOrigins =>
    [
        [new Action<OrchestrationOriginMetadata>(metadata => metadata.SagaId = "saga-1")],
        [new Action<OrchestrationOriginMetadata>(metadata => metadata.OrchestrationInstanceId = "instance-1")],
        [new Action<OrchestrationOriginMetadata>(metadata => metadata.CorrelationId = "corr-1")],
        [new Action<OrchestrationOriginMetadata>(metadata => metadata.TraceId = "trace-1")],
        [new Action<OrchestrationOriginMetadata>(metadata => metadata.StageKey = "stage-1")],
        [new Action<OrchestrationOriginMetadata>(metadata => metadata.TaskKeys = ["task-1"])],
        [new Action<OrchestrationOriginMetadata>(metadata => metadata.TaskExecutionId = "task-execution-1")],
        [new Action<OrchestrationOriginMetadata>(metadata => metadata.DispatchId = "dispatch-1")],
        [new Action<OrchestrationOriginMetadata>(metadata => metadata.Attempt = 1)],
        [new Action<OrchestrationOriginMetadata>(metadata => metadata.Source = "SagaTask")]
    ];

    [Fact]
    public void HasValues_ReturnsFalseWhenAllFieldsAreEmpty()
    {
        var metadata = new OrchestrationOriginMetadata
        {
            SagaId = " ",
            OrchestrationInstanceId = " ",
            CorrelationId = " ",
            TraceId = " ",
            StageKey = " ",
            TaskKeys = [],
            TaskExecutionId = " ",
            DispatchId = " ",
            Source = " "
        };

        Assert.False(metadata.HasValues);
    }

    [Theory]
    [MemberData(nameof(PopulatedOrigins))]
    public void HasValues_ReturnsTrueWhenAnyFieldIsPopulated(Action<OrchestrationOriginMetadata> configure)
    {
        var metadata = new OrchestrationOriginMetadata();

        configure(metadata);

        Assert.True(metadata.HasValues);
    }

    [Fact]
    public void ToJson_WritesOnlyPopulatedValues()
    {
        var metadata = new OrchestrationOriginMetadata
        {
            SagaId = "saga-1",
            OrchestrationInstanceId = " ",
            CorrelationId = "corr-1",
            TaskKeys = [" ", "task-1"],
            Attempt = 2,
            Source = "SagaTask"
        };

        var json = metadata.ToJson();

        Assert.Equal("saga-1", json[nameof(OrchestrationOriginMetadata.SagaId)]!.GetValue<string>());
        Assert.Equal("corr-1", json[nameof(OrchestrationOriginMetadata.CorrelationId)]!.GetValue<string>());
        Assert.Equal("task-1", json[nameof(OrchestrationOriginMetadata.TaskKeys)]![0]!.GetValue<string>());
        Assert.Equal(2, json[nameof(OrchestrationOriginMetadata.Attempt)]!.GetValue<int>());
        Assert.Equal("SagaTask", json[nameof(OrchestrationOriginMetadata.Source)]!.GetValue<string>());
        Assert.False(json.ContainsKey(nameof(OrchestrationOriginMetadata.OrchestrationInstanceId)));
    }

    [Fact]
    public void ToJson_SkipsEmptyTaskArrays()
    {
        var onlyBlankTasks = new OrchestrationOriginMetadata
        {
            TaskKeys = [" "]
        };
        var nullTasks = new OrchestrationOriginMetadata();

        Assert.False(onlyBlankTasks.ToJson().ContainsKey(nameof(OrchestrationOriginMetadata.TaskKeys)));
        Assert.False(nullTasks.ToJson().ContainsKey(nameof(OrchestrationOriginMetadata.TaskKeys)));
    }
}
