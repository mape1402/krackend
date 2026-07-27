using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Krackend.EventSourcing.EntityFrameworkCore.Internal;

internal sealed class KrackendEventStoreModelCustomizer : ModelCustomizer
{
    public KrackendEventStoreModelCustomizer(ModelCustomizerDependencies dependencies)
        : base(dependencies)
    {
    }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        var extension = context.GetService<IDbContextOptions>()
            .FindExtension<KrackendEventStoreOptionsExtension>();

        if (extension is not null)
            modelBuilder.AddKrackendEventStore(extension.Stores);
    }
}
