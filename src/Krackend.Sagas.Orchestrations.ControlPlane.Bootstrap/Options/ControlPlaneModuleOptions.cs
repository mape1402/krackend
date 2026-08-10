using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Bootstrap.Options;

public sealed class ControlPlaneModuleOptions
{
    public string AdminRootPath { get; set; } = "admin";
    public Action<DbContextOptionsBuilder> ConfigureSqlServer { get; set; }
}
