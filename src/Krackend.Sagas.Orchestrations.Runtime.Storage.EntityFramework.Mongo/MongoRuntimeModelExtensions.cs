using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Mongo;

/// <summary>MongoDB-specific mappings chosen by the host.</summary>
public static class MongoRuntimeModelExtensions
{
    /// <summary>Maps runtime and Mule primary keys to MongoDB's document identifier.</summary>
    /// <remarks>Call within the host model configuration. This method does not create, change, or delete indexes.</remarks>
    public static ModelBuilder UseKrackendMongoRuntimeModel(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        MapKey(modelBuilder, "Mule.MuleReplay", "RequestId");
        const string prefix = "Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure.";
        MapKey(modelBuilder, prefix + "SagaStatisticsObservation", "Id");
        MapKey(modelBuilder, prefix + "SagaStatisticsProjection", "Id");
        MapKey(modelBuilder, prefix + "SagaStatisticsBackfillCheckpoint", "Id");
        MapKey(modelBuilder, prefix + "SagaFailureEvent", "Id");
        MapKey(modelBuilder, prefix + "SagaFailureBackfillCheckpoint", "Id");
        return modelBuilder;
    }

    private static void MapKey(ModelBuilder modelBuilder, string entityName, string propertyName)
    {
        var entity = modelBuilder.Model.FindEntityType(entityName)
            ?? throw new InvalidOperationException($"Runtime entity '{entityName}' is not mapped.");
        if (entity.FindProperty(propertyName) is null)
            throw new InvalidOperationException($"Runtime key '{entityName}.{propertyName}' is not mapped.");
        modelBuilder.Entity(entity.ClrType).Property(propertyName).HasElementName("_id");
    }
}
