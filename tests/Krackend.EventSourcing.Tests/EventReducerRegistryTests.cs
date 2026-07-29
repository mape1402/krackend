using Krackend.EventSourcing.Core;

namespace Krackend.EventSourcing.Tests;

public sealed class EventReducerRegistryTests
{
    [Fact]
    public void Apply_throws_when_reducer_is_missing()
    {
        var registry = new EventReducerRegistry();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.Apply(TestState.Empty, new TestEvent()));

        Assert.Contains(typeof(TestState).FullName!, exception.Message);
        Assert.Contains(typeof(TestEvent).FullName!, exception.Message);
    }

    private sealed record TestState
    {
        public static TestState Empty { get; } = new();
    }

    private sealed record TestEvent;
}
