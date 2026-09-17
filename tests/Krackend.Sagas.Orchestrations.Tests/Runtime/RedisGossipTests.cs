using System.Text.Json;
using System.Reflection;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RedisGossipTests
{
    [Fact]
    public async Task PublisherPublishesSerializedArtifactReadyMessageWhenEnabled()
    {
        var connectionFactory = Substitute.For<IRedisRuntimeGossipConnectionFactory>();
        var connection = Substitute.For<IConnectionMultiplexer>();
        var subscriber = Substitute.For<ISubscriber>();
        RedisValue? publishedPayload = null;
        RedisChannel? publishedChannel = null;

        connectionFactory.GetConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(connection);
        connection.GetSubscriber(Arg.Any<object>())
            .Returns(subscriber);
        subscriber.PublishAsync(
                Arg.Do<RedisChannel>(channel => publishedChannel = channel),
                Arg.Do<RedisValue>(payload => publishedPayload = payload),
                Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(1L));

        var publisher = new RedisRuntimeArtifactReadyGossipPublisher(
            connectionFactory,
            Options.Create(new RuntimeGossipOptions
            {
                Enabled = true,
                ChannelName = "runtime.artifacts"
            }));
        var message = new RuntimeArtifactReadyGossipMessage
        {
            ArtifactId = "artifact-1",
            OrchestrationDefinitionKey = "sales.sale.created",
            Version = "1.0.0",
            IngressGeneration = 5,
            OccurredOnUtc = DateTime.UtcNow
        };

        await publisher.PublishAsync(message);

        Assert.Equal("runtime.artifacts", publishedChannel?.ToString());
        Assert.NotNull(publishedPayload);
        var json = JsonDocument.Parse(publishedPayload.Value.ToString());
        Assert.Equal("artifact-1", json.RootElement.GetProperty("artifactId").GetString());
        Assert.Equal("sales.sale.created", json.RootElement.GetProperty("orchestrationDefinitionKey").GetString());
        Assert.Equal(5, json.RootElement.GetProperty("ingressGeneration").GetInt64());
    }

    [Fact]
    public async Task PublisherSkipsRedisWhenGossipIsDisabled()
    {
        var connectionFactory = Substitute.For<IRedisRuntimeGossipConnectionFactory>();
        var publisher = new RedisRuntimeArtifactReadyGossipPublisher(
            connectionFactory,
            Options.Create(new RuntimeGossipOptions
            {
                Enabled = false,
                ChannelName = "runtime.artifacts"
            }));

        await publisher.PublishAsync(new RuntimeArtifactReadyGossipMessage
        {
            ArtifactId = "artifact-1",
            OrchestrationDefinitionKey = "sales.sale.created",
            Version = "1.0.0",
            IngressGeneration = 1
        });

        await connectionFactory.DidNotReceive().GetConnectionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListenerDoesNotConnectWhenGossipIsDisabled()
    {
        var connectionFactory = Substitute.For<IRedisRuntimeGossipConnectionFactory>();
        var listener = CreateListener(
            new ServiceCollection().BuildServiceProvider(),
            connectionFactory,
            new RuntimeGossipOptions
            {
                Enabled = false,
                ChannelName = "runtime.artifacts"
            });

        await listener.StartAsync(CancellationToken.None);
        await listener.StopAsync(CancellationToken.None);

        await connectionFactory.DidNotReceive().GetConnectionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListenerSubscribesAndUnsubscribesWhenGossipIsEnabled()
    {
        var connectionFactory = Substitute.For<IRedisRuntimeGossipConnectionFactory>();
        var connection = Substitute.For<IConnectionMultiplexer>();
        var subscriber = Substitute.For<ISubscriber>();
        Action<RedisChannel, RedisValue>? subscriptionHandler = null;
        connectionFactory.GetConnectionAsync(Arg.Any<CancellationToken>()).Returns(connection);
        connection.GetSubscriber(Arg.Any<object>()).Returns(subscriber);
        subscriber
            .SubscribeAsync(
                Arg.Any<RedisChannel>(),
                Arg.Do<Action<RedisChannel, RedisValue>>(handler => subscriptionHandler = handler),
                Arg.Any<CommandFlags>())
            .Returns(Task.CompletedTask);
        subscriber
            .UnsubscribeAsync(Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>())
            .Returns(Task.CompletedTask);
        var listener = CreateListener(
            new ServiceCollection().BuildServiceProvider(),
            connectionFactory,
            new RuntimeGossipOptions
            {
                Enabled = true,
                ChannelName = "runtime.artifacts"
            });

        await listener.StartAsync(CancellationToken.None);
        subscriptionHandler!(RedisChannel.Literal("runtime.artifacts"), RedisValue.Null);
        await listener.StopAsync(CancellationToken.None);

        await subscriber.Received(1).SubscribeAsync(
            Arg.Is<RedisChannel>(channel => channel.ToString() == "runtime.artifacts"),
            Arg.Any<Action<RedisChannel, RedisValue>>(),
            Arg.Any<CommandFlags>());
        await subscriber.Received(1).UnsubscribeAsync(
            Arg.Is<RedisChannel>(channel => channel.ToString() == "runtime.artifacts"),
            Arg.Any<Action<RedisChannel, RedisValue>>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task ListenerHandlesValidEmptyAndInvalidMessagesWithoutLeakingExceptions()
    {
        var handler = Substitute.For<IRuntimeArtifactReadyGossipHandler>();
        var services = new ServiceCollection();
        services.AddScoped(_ => handler);
        await using var provider = services.BuildServiceProvider();
        var listener = CreateListener(
            provider,
            Substitute.For<IRedisRuntimeGossipConnectionFactory>(),
            new RuntimeGossipOptions
            {
                Enabled = true,
                ChannelName = "runtime.artifacts"
            });
        var message = new RuntimeArtifactReadyGossipMessage
        {
            ArtifactId = "artifact-1",
            OrchestrationDefinitionKey = "sales.sale.created",
            Version = "1.0.0",
            IngressGeneration = 3,
            OccurredOnUtc = DateTime.UtcNow
        };

        await InvokeHandleMessageAsync(listener, RedisValue.Null);
        await InvokeHandleMessageAsync(listener, "null");
        await InvokeHandleMessageAsync(listener, "{not-json");
        await InvokeHandleMessageAsync(listener, JsonSerializer.Serialize(message, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        await handler.Received(1).HandleAsync(
            Arg.Is<RuntimeArtifactReadyGossipMessage>(payload =>
                payload.ArtifactId == "artifact-1" &&
                payload.IngressGeneration == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RedisConnectionFactoryRejectsDisabledOrIncompleteConfigurationBeforeConnecting()
    {
        var disabledServices = new ServiceCollection();
        disabledServices.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        disabledServices.AddLogging();
        disabledServices
            .AddKrackendOrchestrationsRuntime()
            .AddRedisGossip(new ConfigurationBuilder().Build());
        disabledServices.Configure<RuntimeGossipOptions>(options => options.Enabled = false);

        await using (var disabledProvider = disabledServices.BuildServiceProvider())
        {
            var factory = disabledProvider.GetRequiredService<IRedisRuntimeGossipConnectionFactory>();
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => factory.GetConnectionAsync());
            Assert.Contains("disabled", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        var missingConnectionServices = new ServiceCollection();
        missingConnectionServices.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        missingConnectionServices.AddLogging();
        missingConnectionServices
            .AddKrackendOrchestrationsRuntime()
            .AddRedisGossip(new ConfigurationBuilder().Build());
        missingConnectionServices.Configure<RuntimeGossipOptions>(options =>
        {
            options.Enabled = true;
            options.RedisConnectionString = string.Empty;
        });

        await using var missingConnectionProvider = missingConnectionServices.BuildServiceProvider();
        var missingConnectionFactory = missingConnectionProvider.GetRequiredService<IRedisRuntimeGossipConnectionFactory>();
        var missingConnectionException = await Assert.ThrowsAsync<InvalidOperationException>(() => missingConnectionFactory.GetConnectionAsync());
        Assert.Contains("connection string", missingConnectionException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RedisConnectionFactoryReusesCachedConnectionsBeforeAndAfterWaitingForGate()
    {
        var cachedConnection = Substitute.For<IConnectionMultiplexer>();
        cachedConnection.IsConnected.Returns(true);
        await using var factoryProvider = CreateFactoryProvider();
        var factory = factoryProvider.GetRequiredService<IRedisRuntimeGossipConnectionFactory>();
        SetPrivateField(factory, "_connection", cachedConnection);

        var reused = await factory.GetConnectionAsync();

        Assert.Same(cachedConnection, reused);

        var refreshedConnection = Substitute.For<IConnectionMultiplexer>();
        refreshedConnection.IsConnected.Returns(true);
        await using var waitingFactoryProvider = CreateFactoryProvider();
        var waitingFactory = waitingFactoryProvider.GetRequiredService<IRedisRuntimeGossipConnectionFactory>();
        var gate = GetPrivateField<SemaphoreSlim>(waitingFactory, "_gate");
        await gate.WaitAsync();
        var pending = waitingFactory.GetConnectionAsync();

        SetPrivateField(waitingFactory, "_connection", refreshedConnection);
        gate.Release();
        var refreshed = await pending;

        Assert.Same(refreshedConnection, refreshed);

        ((IDisposable)factory).Dispose();
        ((IDisposable)waitingFactory).Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => factory.GetConnectionAsync());
    }

    [Fact]
    public async Task RedisGossipComponentsValidateConstructorArgumentsAndMessage()
    {
        var connectionFactory = Substitute.For<IRedisRuntimeGossipConnectionFactory>();
        var options = Options.Create(new RuntimeGossipOptions { Enabled = true });
        var provider = new ServiceCollection().BuildServiceProvider();

        Assert.Throws<ArgumentNullException>(() => new RedisRuntimeArtifactReadyGossipPublisher(null!, options));
        Assert.Throws<ArgumentNullException>(() => new RedisRuntimeArtifactReadyGossipPublisher(connectionFactory, null!));
        Assert.Throws<ArgumentNullException>(() => new RedisRuntimeArtifactReadyGossipListener(
            null!,
            connectionFactory,
            options,
            NullLogger<RedisRuntimeArtifactReadyGossipListener>.Instance));
        Assert.Throws<ArgumentNullException>(() => new RedisRuntimeArtifactReadyGossipListener(
            provider.GetRequiredService<IServiceScopeFactory>(),
            null!,
            options,
            NullLogger<RedisRuntimeArtifactReadyGossipListener>.Instance));
        Assert.Throws<ArgumentNullException>(() => new RedisRuntimeArtifactReadyGossipListener(
            provider.GetRequiredService<IServiceScopeFactory>(),
            connectionFactory,
            null!,
            NullLogger<RedisRuntimeArtifactReadyGossipListener>.Instance));
        Assert.Throws<ArgumentNullException>(() => new RedisRuntimeArtifactReadyGossipListener(
            provider.GetRequiredService<IServiceScopeFactory>(),
            connectionFactory,
            options,
            null!));

        var publisher = new RedisRuntimeArtifactReadyGossipPublisher(connectionFactory, options);
        await Assert.ThrowsAsync<ArgumentNullException>(() => publisher.PublishAsync(null!));
    }

    [Fact]
    public void RuntimeArtifactReadyGossipMessageCopiesRuntimeArtifactIdentity()
    {
        var artifact = new RuntimeOrchestrationArtifact
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            ArtifactType = "orchestration-version-snapshot",
            Version = new SemanticVersion(2, 1, 0),
            ArtifactChecksum = new Checksum("sha256:test"),
            ArtifactPayload = JsonSerializer.SerializeToNode(new { key = "sales.sale.created" })!,
            IngressGeneration = 7
        };

        var message = RuntimeArtifactReadyGossipMessage.FromArtifact(artifact);

        Assert.Equal(artifact.Id.ToString(), message.ArtifactId);
        Assert.Equal("sales.sale.created", message.OrchestrationDefinitionKey);
        Assert.Equal("2.1.0", message.Version);
        Assert.Equal(7, message.IngressGeneration);
        Assert.True(message.OccurredOnUtc <= DateTime.UtcNow);
        Assert.Throws<ArgumentNullException>(() => RuntimeArtifactReadyGossipMessage.FromArtifact(null!));
    }

    private static RedisRuntimeArtifactReadyGossipListener CreateListener(
        ServiceProvider provider,
        IRedisRuntimeGossipConnectionFactory connectionFactory,
        RuntimeGossipOptions options)
        => new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            connectionFactory,
            Options.Create(options),
            NullLogger<RedisRuntimeArtifactReadyGossipListener>.Instance);

    private static Task InvokeHandleMessageAsync(
        RedisRuntimeArtifactReadyGossipListener listener,
        RedisValue payload)
    {
        var method = typeof(RedisRuntimeArtifactReadyGossipListener).GetMethod(
            "HandleMessageAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)method.Invoke(listener, [payload])!;
    }

    private static ServiceProvider CreateFactoryProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Krackend:Sagas:Orchestrations:Runtime:Gossip:Enabled"] = "true",
                ["Krackend:Sagas:Orchestrations:Runtime:Gossip:RedisConnectionString"] = "localhost:6379"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services
            .AddKrackendOrchestrationsRuntime()
            .AddRedisGossip(configuration);
        services.Configure<RuntimeGossipOptions>(options =>
        {
            options.Enabled = true;
            options.RedisConnectionString = "localhost:6379";
        });

        return services.BuildServiceProvider();
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
        => (T)instance.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance)!;

    private static void SetPrivateField(object instance, string fieldName, object value)
        => instance.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(instance, value);
}
