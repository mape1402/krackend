namespace Krackend.Sagas.Orchestrations.Tests.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Repositories;
using Microsoft.EntityFrameworkCore;

public sealed class OrchestrationVersionRepositoryBranchTests
{
    [Fact]
    public void GetDraftSlotUsesAnIndependentClosedSlotForNonDraftVersions()
    {
        var orchestrationId = Id.New();
        var versionId = Id.New();

        var slot = InvokePrivateStatic<string>(
            "GetDraftSlot",
            orchestrationId,
            versionId,
            OrchestrationVersionStatus.Deployed);

        Assert.Equal($"closed:{versionId}", slot);
    }

    [Fact]
    public void DraftSlotConstraintDetectionRecognizesBothProviderErrorLocations()
    {
        var innerViolation = new DbUpdateException("storage update failed", new InvalidOperationException("DraftSlot is unique"));
        var outerViolation = new DbUpdateException("DraftSlot is unique");
        var unrelated = new DbUpdateException("another unique index failed");

        Assert.True(InvokePrivateStatic<bool>("IsOpenDraftConstraintViolation", innerViolation));
        Assert.True(InvokePrivateStatic<bool>("IsOpenDraftConstraintViolation", outerViolation));
        Assert.False(InvokePrivateStatic<bool>("IsOpenDraftConstraintViolation", unrelated));
    }

    private static T InvokePrivateStatic<T>(string methodName, params object?[] arguments)
    {
        var method = typeof(OrchestrationVersionRepository).GetMethod(
            methodName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        return (T)method.Invoke(null, arguments)!;
    }
}
