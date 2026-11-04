namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Repositories;

using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

internal static class DesignerConcurrencyGuard
{
    public static void EnsureTransformation(string expected, TransformationDefinition value)
        => Ensure(expected, (value?.Configuration as DslTransformationConfiguration)?.Dsl);

    public static void EnsureCondition(string expected, ExecutionCondition value)
        => Ensure(expected, (value?.Configuration as DslConditionConfiguration)?.Expression.ToString());

    private static void Ensure(string expected, string dsl)
    {
        if (string.IsNullOrWhiteSpace(expected) ||
            !string.Equals(expected, OrchestrationDesignerRevision.FromDsl(dsl), StringComparison.Ordinal))
            throw new OrchestrationDesignerConcurrencyException();
    }

    public static async Task SaveAsync(ControlPlaneDbContext context, bool guarded, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception) when (guarded)
        {
            throw new OrchestrationDesignerConcurrencyException(exception);
        }
    }
}
