namespace Krackend.Sagas.Orchestrations.Tests.Design;

using System.Reflection;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

public sealed class ValidationRulesReflectionTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("1.0", false)]
    [InlineData("1.x.0", false)]
    [InlineData("1.2.3", true)]
    public void InternalValidationRulesValidateSemanticVersions(string value, bool expected)
    {
        var method = typeof(IOrchestrationSchemaContextBuilder).Assembly
            .GetType("Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.ValidationRules", true)!
            .GetMethod("IsSemanticVersion", BindingFlags.Public | BindingFlags.Static)!;

        var result = (bool)method.Invoke(null, [value])!;

        Assert.Equal(expected, result);
    }

    [Fact]
    public void InternalValidationRulesValidateUlids()
    {
        var method = typeof(IOrchestrationSchemaContextBuilder).Assembly
            .GetType("Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.ValidationRules", true)!
            .GetMethod("IsUlid", BindingFlags.Public | BindingFlags.Static)!;

        Assert.True((bool)method.Invoke(null, [Ulid.NewUlid().ToString()])!);
        Assert.False((bool)method.Invoke(null, ["bad-ulid"])!);
    }
}
