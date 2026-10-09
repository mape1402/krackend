namespace Krackend.Sagas.Orchestrations.Tests.Client;

public sealed class OrchestrationOperationExecutionContextTests
{
    [Fact]
    public void ExecutionContextReportsDefaultsUntilOperationStarts()
    {
        var context = CreateContext();

        Assert.Equal(default, GetStartedOnUtc(context));
        Assert.Null(GetRequestType(context));
    }

    [Fact]
    public void ExecutionContextTracksAndClearsCurrentOperation()
    {
        var context = CreateContext();

        Invoke(context, "Start", typeof(SampleRequest));

        Assert.Equal(typeof(SampleRequest), GetRequestType(context));
        Assert.NotEqual(default, GetStartedOnUtc(context));

        Invoke(context, "Clear");

        Assert.Equal(default, GetStartedOnUtc(context));
        Assert.Null(GetRequestType(context));
    }

    private static object CreateContext()
    {
        var type = typeof(Krackend.Sagas.Orchestrations.Client.Operations.IOrchestrationOperationClient).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.Client.Publishing.DefaultOrchestrationOperationExecutionContext");
        Assert.NotNull(type);
        var instance = Activator.CreateInstance(type);
        Assert.NotNull(instance);
        return instance;
    }

    private static DateTime GetStartedOnUtc(object context)
        => Assert.IsType<DateTime>(context.GetType().GetProperty("StartedOnUtc")?.GetValue(context));

    private static Type? GetRequestType(object context)
        => context.GetType().GetProperty("RequestType")?.GetValue(context) as Type;

    private static void Invoke(object context, string methodName, params object[] args)
    {
        var method = context.GetType().GetMethod(methodName);
        Assert.NotNull(method);
        method.Invoke(context, args);
    }

    private sealed class SampleRequest;
}
