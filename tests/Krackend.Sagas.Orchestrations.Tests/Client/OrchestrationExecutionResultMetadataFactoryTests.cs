namespace Krackend.Sagas.Orchestrations.Tests.Client;

using System.Reflection;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Microsoft.Extensions.DependencyInjection;

public sealed class OrchestrationExecutionResultMetadataFactoryTests
{
    [Fact]
    public void FactoryCoversNullTypesOptionsMetadataAndConstructorGuards()
    {
        using var provider = CreateProvider();
        var factory = GetFactory(provider);

        var nullSuccess = InvokeFactory<OrchestrationExecutionResultMetadata>(
            factory,
            "CreateSuccess",
            [typeof(Type), typeof(Type), typeof(OrchestrationOperationOptions)],
            [null, null, null]);
        var explicitSuccess = InvokeFactory<OrchestrationExecutionResultMetadata>(
            factory,
            "CreateSuccess",
            [typeof(Type), typeof(Type), typeof(OrchestrationOperationOptions)],
            [
                typeof(string),
                typeof(int),
                new OrchestrationOperationOptions
                {
                    ServiceName = "orders-api",
                    OperationName = "orders.reserve",
                    Metadata =
                    {
                        ["null-value"] = null,
                        ["value"] = JsonValue.Create("metadata")
                    }
                }
            ]);
        GetExecutionContext(provider).GetType().GetMethod("Start")!.Invoke(GetExecutionContext(provider), [typeof(string)]);
        var fallbackOperationSuccess = InvokeFactory<OrchestrationExecutionResultMetadata>(
            factory,
            "CreateSuccess",
            [typeof(Type), typeof(Type), typeof(OrchestrationOperationOptions)],
            [typeof(string), typeof(int), new OrchestrationOperationOptions { OperationName = " " }]);
        var detailsFailure = InvokeFactory<OrchestrationExecutionResultMetadata>(
            factory,
            "CreateFailure",
            [typeof(Type), typeof(Exception), typeof(OrchestrationOperationOptions)],
            [
                typeof(string),
                new InvalidOperationException("boom"),
                new OrchestrationOperationOptions
                {
                    ServiceName = "orders-api",
                    OperationName = "orders.fail",
                    Metadata =
                    {
                        ["failure"] = JsonValue.Create("metadata")
                    }
                }
            ]);
        var nullDetailsFailure = InvokeFactory<OrchestrationExecutionResultMetadata>(
            factory,
            "CreateFailure",
            [typeof(Type), typeof(Exception), typeof(OrchestrationOperationOptions)],
            [null, null, null]);

        Assert.True(nullSuccess.Succeeded);
        Assert.Null(nullSuccess.RequestType);
        Assert.Null(nullSuccess.ResponseType);
        Assert.Empty(nullSuccess.Metadata);
        Assert.Equal("orders.reserve", explicitSuccess.OperationName);
        Assert.True(explicitSuccess.Metadata.ContainsKey("null-value"));
        Assert.Null(explicitSuccess.Metadata["null-value"]);
        Assert.Equal("metadata", explicitSuccess.Metadata["value"]!.GetValue<string>());
        Assert.Equal(typeof(string).FullName, fallbackOperationSuccess.OperationName);
        Assert.NotEqual(default, fallbackOperationSuccess.StartedOnUtc);
        Assert.Equal("boom", detailsFailure.ErrorMessage);
        Assert.Equal(typeof(InvalidOperationException).FullName, detailsFailure.ErrorType);
        Assert.Equal("orders.fail", detailsFailure.OperationName);
        Assert.Equal("metadata", detailsFailure.Metadata["failure"]!.GetValue<string>());
        Assert.Null(nullDetailsFailure.ErrorMessage);
        Assert.Null(nullDetailsFailure.ErrorType);
        AssertConstructorGuards(provider);
    }

    [Fact]
    public void FactoryCoversFailureMessageFallbacks()
    {
        using var provider = CreateProvider(options =>
        {
            options.IncludeExceptionDetails = false;
            options.RedactedExceptionMessage = " ";
        });
        var factory = GetFactory(provider);

        var nullExceptionFailure = InvokeFactory<OrchestrationExecutionResultMetadata>(
            factory,
            "CreateFailure",
            [typeof(Type), typeof(Exception), typeof(OrchestrationOperationOptions)],
            [null, null, null]);
        var fallbackFailure = InvokeFactory<OrchestrationExecutionResultMetadata>(
            factory,
            "CreateFailure",
            [typeof(Type), typeof(Exception), typeof(OrchestrationOperationOptions)],
            [typeof(string), new InvalidOperationException("boom"), new OrchestrationOperationOptions { OperationName = " " }]);

        Assert.False(nullExceptionFailure.Succeeded);
        Assert.Equal("UnhandledException", nullExceptionFailure.ErrorCode);
        Assert.Equal("UnhandledException", nullExceptionFailure.ErrorMessage);
        Assert.Null(nullExceptionFailure.ErrorType);
        Assert.Equal(typeof(string).FullName, fallbackFailure.OperationName);
        Assert.Equal("UnhandledException", fallbackFailure.ErrorMessage);

        using var redactedProvider = CreateProvider(options =>
        {
            options.IncludeExceptionDetails = false;
            options.RedactedExceptionMessage = "Hidden failure.";
        });
        var redactedFailure = InvokeFactory<OrchestrationExecutionResultMetadata>(
            GetFactory(redactedProvider),
            "CreateFailure",
            [typeof(Type), typeof(Exception), typeof(OrchestrationOperationOptions)],
            [typeof(string), new InvalidOperationException("boom"), null]);
        Assert.Equal("Hidden failure.", redactedFailure.ErrorMessage);
    }

    private static ServiceProvider CreateProvider(Action<Krackend.Sagas.Orchestrations.Client.Errors.OrchestrationClientErrorMappingOptions>? configure = null)
    {
        var services = new ServiceCollection();
        if (configure is null)
        {
            services.AddKrackendOrchestrationsClient();
        }
        else
        {
            services.AddKrackendOrchestrationsClient(configure);
        }

        return services.BuildServiceProvider();
    }

    private static object GetFactory(IServiceProvider provider)
    {
        var factoryType = typeof(OrchestrationOperationOptions).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.Client.Publishing.IOrchestrationExecutionResultMetadataFactory")!;
        return provider.GetRequiredService(factoryType);
    }

    private static object GetExecutionContext(IServiceProvider provider)
    {
        var contextType = typeof(OrchestrationOperationOptions).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.Client.Publishing.IOrchestrationOperationExecutionContext")!;
        return provider.GetRequiredService(contextType);
    }

    private static void AssertConstructorGuards(IServiceProvider provider)
    {
        var factoryType = typeof(OrchestrationOperationOptions).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.Client.Publishing.DefaultOrchestrationExecutionResultMetadataFactory")!;
        var constructor = factoryType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
        var arguments = constructor.GetParameters()
            .Select(parameter => provider.GetRequiredService(parameter.ParameterType))
            .ToArray();

        for (var index = 0; index < arguments.Length; index++)
        {
            var candidate = arguments.ToArray();
            candidate[index] = null!;

            var exception = Assert.Throws<TargetInvocationException>(() => constructor.Invoke(candidate));
            Assert.IsType<ArgumentNullException>(exception.InnerException);
        }
    }

    private static TResult InvokeFactory<TResult>(
        object factory,
        string methodName,
        Type[] parameterTypes,
        object?[] arguments)
    {
        var method = factory.GetType().GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: parameterTypes,
            modifiers: null)!;
        return (TResult)method.Invoke(factory, arguments)!;
    }
}
