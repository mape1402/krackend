namespace Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Data.SqlClient;
using RabbitMQ.Client;
using System.Net.Sockets;

public sealed class RuntimeRealInfrastructureFixture : IAsyncLifetime
{
    private const string SqlPassword = "Krackend_12345!";
    private IContainer? _rabbitMq;
    private IContainer? _redis;
    private IContainer? _sqlServer;

    public string RabbitMqConnectionString { get; private set; } = string.Empty;

    public string RedisConnectionString { get; private set; } = string.Empty;

    public string MasterSqlConnectionString { get; private set; } = string.Empty;

    public async Task StopRabbitMqAsync(CancellationToken cancellationToken = default)
    {
        if (_rabbitMq is null)
        {
            throw new InvalidOperationException("RabbitMQ container is not running.");
        }

        await _rabbitMq.StopAsync(cancellationToken);
    }

    public async Task StartRabbitMqAsync(CancellationToken cancellationToken = default)
    {
        if (_rabbitMq is null)
        {
            throw new InvalidOperationException("RabbitMQ container is not available.");
        }

        await _rabbitMq.StartAsync(cancellationToken);
        RabbitMqConnectionString = $"amqp://guest:guest@localhost:{_rabbitMq.GetMappedPublicPort(5672)}";
        await WaitForRabbitMqAsync(cancellationToken);
    }

    public async Task StopRedisAsync(CancellationToken cancellationToken = default)
    {
        if (_redis is null)
        {
            throw new InvalidOperationException("Redis container is not running.");
        }

        await _redis.StopAsync(cancellationToken);
    }

    public async Task StartRedisAsync(CancellationToken cancellationToken = default)
    {
        if (_redis is null)
        {
            throw new InvalidOperationException("Redis container is not available.");
        }

        await _redis.StartAsync(cancellationToken);
        RedisConnectionString = $"localhost:{_redis.GetMappedPublicPort(6379)}";
        await WaitForTcpPortAsync(_redis.GetMappedPublicPort(6379), cancellationToken);
    }

    public async Task StopSqlServerAsync(CancellationToken cancellationToken = default)
    {
        if (_sqlServer is null)
        {
            throw new InvalidOperationException("SQL Server container is not running.");
        }

        await _sqlServer.StopAsync(cancellationToken);
    }

    public async Task StartSqlServerAsync(CancellationToken cancellationToken = default)
    {
        if (_sqlServer is null)
        {
            throw new InvalidOperationException("SQL Server container is not available.");
        }

        await _sqlServer.StartAsync(cancellationToken);
        MasterSqlConnectionString = BuildSqlConnectionString("master");
        await WaitForSqlServerAsync(cancellationToken);
    }

    public async Task InitializeAsync()
    {
        _rabbitMq = new ContainerBuilder()
            .WithImage("rabbitmq:3-management")
            .WithPortBinding(5672, true)
            .WithPortBinding(15672, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5672))
            .Build();

        _redis = new ContainerBuilder()
            .WithImage("redis:7-alpine")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379))
            .Build();

        _sqlServer = new ContainerBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("MSSQL_SA_PASSWORD", SqlPassword)
            .WithPortBinding(1433, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(1433))
            .Build();

        await Task.WhenAll(
            _rabbitMq.StartAsync(),
            _redis.StartAsync(),
            _sqlServer.StartAsync());

        RabbitMqConnectionString = $"amqp://guest:guest@localhost:{_rabbitMq.GetMappedPublicPort(5672)}";
        RedisConnectionString = $"localhost:{_redis.GetMappedPublicPort(6379)}";
        MasterSqlConnectionString = BuildSqlConnectionString("master");

        await WaitForRabbitMqAsync();
        await WaitForSqlServerAsync();
    }

    public string CreateDatabaseName()
        => $"KrackendRuntimeE2E_{Guid.NewGuid():N}";

    public string BuildSqlConnectionString(string databaseName)
    {
        if (_sqlServer is null)
        {
            throw new InvalidOperationException("SQL Server container is not running.");
        }

        return $"Server=localhost,{_sqlServer.GetMappedPublicPort(1433)};Database={databaseName};User Id=sa;Password={SqlPassword};TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True";
    }

    public async Task DisposeAsync()
    {
        if (_rabbitMq is not null)
        {
            await _rabbitMq.DisposeAsync();
        }

        if (_redis is not null)
        {
            await _redis.DisposeAsync();
        }

        if (_sqlServer is not null)
        {
            await _sqlServer.DisposeAsync();
        }
    }

    private Task WaitForSqlServerAsync()
        => WaitForSqlServerAsync(CancellationToken.None);

    private Task WaitForRabbitMqAsync()
        => WaitForRabbitMqAsync(CancellationToken.None);

    private async Task WaitForRabbitMqAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(2);
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(RabbitMqConnectionString)
                };
                using var connection = await factory.CreateConnectionAsync(cancellationToken);
                return;
            }
            catch (Exception exception)
            {
                lastError = exception;
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }

        throw new TimeoutException("RabbitMQ test container did not become available.", lastError);
    }

    private async Task WaitForSqlServerAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(2);
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var connection = new SqlConnection(MasterSqlConnectionString);
                await connection.OpenAsync(cancellationToken);
                return;
            }
            catch (Exception exception)
            {
                lastError = exception;
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        throw new TimeoutException("SQL Server test container did not become available.", lastError);
    }

    private static async Task WaitForTcpPortAsync(int port, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(1);
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync("localhost", port, cancellationToken);
                return;
            }
            catch (Exception exception)
            {
                lastError = exception;
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
        }

        throw new TimeoutException($"TCP port '{port}' did not become available.", lastError);
    }
}
