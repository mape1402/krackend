namespace Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;

using System.Text.Json;

internal static class PollingAssert
{
    public static async Task<T> EventuallyAsync<T>(
        Func<Task<T>> read,
        Func<T, bool> predicate,
        string failureMessage,
        TimeSpan? timeout = null,
        TimeSpan? interval = null)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(predicate);

        var deadline = DateTimeOffset.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(45));
        var delay = interval ?? TimeSpan.FromMilliseconds(250);
        T? lastValue = default;

        while (DateTimeOffset.UtcNow < deadline)
        {
            lastValue = await read();
            if (predicate(lastValue))
            {
                return lastValue;
            }

            await Task.Delay(delay);
        }

        throw new TimeoutException($"{failureMessage}. Last observed value: {Format(lastValue)}");
    }

    private static string Format<T>(T value)
    {
        if (value is null)
        {
            return "<null>";
        }

        try
        {
            return JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (Exception)
        {
            return value.ToString() ?? value.GetType().FullName ?? "<unknown>";
        }
    }
}
