namespace Krackend.Sagas.Orchestrations.Tests.Storage;

using System.Reflection;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore.ChangeTracking;

public sealed class StringListValueComparerTests
{
    [Fact]
    public void SnapshotCreatesAnEmptyListForANullSource()
    {
        var deserialize = GetDeserializeMethod();

        var snapshot = deserialize.Invoke(null, [" "]);

        Assert.Empty(Assert.IsType<List<string>>(snapshot));
    }

    [Fact]
    public void SnapshotCreatesAnIndependentCopyForAList()
    {
        var comparer = CreateComparer();
        var values = new List<string> { "one" };

        var snapshot = comparer.Snapshot(values);
        values.Add("two");

        Assert.Equal(["one"], Assert.IsType<List<string>>(snapshot));
    }

    [Fact]
    public void DeserializeCreatesAnEmptyListForJsonNull()
    {
        var snapshot = GetDeserializeMethod().Invoke(null, ["null"]);

        Assert.Empty(Assert.IsType<List<string>>(snapshot));
    }

    private static ValueComparer CreateComparer()
    {
        var type = typeof(ControlPlaneDbContext).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure.StringListValueComparer",
            throwOnError: true)!;
        return (ValueComparer)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, null, null)!;
    }

    private static MethodInfo GetDeserializeMethod()
    {
        return CreateComparer().GetType().GetMethod(
            "Deserialize",
            BindingFlags.Static | BindingFlags.NonPublic)!;
    }
}
