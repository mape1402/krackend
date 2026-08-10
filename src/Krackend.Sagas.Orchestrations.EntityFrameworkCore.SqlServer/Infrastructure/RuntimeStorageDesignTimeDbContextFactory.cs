using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Infrastructure;

public sealed class RuntimeStorageDesignTimeDbContextFactory : IDesignTimeDbContextFactory<RuntimeStorageDbContext>
{
    public RuntimeStorageDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RuntimeStorageDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=KrackendSagasOrchestrationsRuntimeDesignTime;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new RuntimeStorageDbContext(options);
    }
}
