using Microsoft.EntityFrameworkCore;
using Mule.EntityFrameworkCore;

namespace Krackend.Demo;

public sealed class DemoOperation
{
    public string Id { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string TestRunId { get; set; } = "";
    public string SaleId { get; set; } = "";
    public string Operation { get; set; } = "";
    public string InputHash { get; set; } = "";
    public string ResultJson { get; set; } = "";
    public DateTime CompletedOnUtc { get; set; }
}
public sealed class DemoSale
{
    public string Id { get; set; } = "";
    public string CustomerId { get; set; } = "demo-customer";
    public string TestRunId { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string LinesJson { get; set; } = "[]";
    public decimal Total { get; set; }
    public decimal ReturnedAmount { get; set; }
    public string ReturnedLinesJson { get; set; } = "[]";
    public DateTime? ExpiresOnUtc { get; set; }
    public string? ResolutionRequestId { get; set; }
    public string OriginRequestId { get; set; } = "";
    public string? PreviousStatus { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
public sealed class DemoStock
{
    public string Id { get; set; } = "";
    public string TestRunId { get; set; } = "";
    public int OnHand { get; set; }
    public int Reserved { get; set; }
}
public sealed class DemoReservation
{
    public string Id { get; set; } = "";
    public string TestRunId { get; set; } = "";
    public string SaleId { get; set; } = "";
    public string LinesJson { get; set; } = "[]";
    public string Status { get; set; } = "Reserved";
    public DateTime? ExpiresOnUtc { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
public sealed class DemoPayment
{
    public string Id { get; set; } = "";
    public string TestRunId { get; set; } = "";
    public string IntentStatus { get; set; } = "None";
    public decimal Captured { get; set; }
    public decimal ExpectedTotal { get; set; }
    public decimal Refunded { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
public sealed class DemoBusinessEntry
{
    public string Id { get; set; } = "";
    public string TestRunId { get; set; } = "";
    public string SaleId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string DataJson { get; set; } = "";
    public DateTime CreatedOnUtc { get; set; } = DateTime.UtcNow;
}
public sealed class DemoEmail
{
    public string Id { get; set; } = "";
    public string TestRunId { get; set; } = "";
    public string SaleId { get; set; } = "";
    public string Recipient { get; set; } = "";
    public string Body { get; set; } = "";
    public string Status { get; set; } = "Queued";
    public int Attempts { get; set; }
    public DateTime? DeliveredOnUtc { get; set; }
}
public sealed class DemoDbContext(DbContextOptions<DemoDbContext> options) : DbContext(options)
{
    public DbSet<DemoOperation> Operations => Set<DemoOperation>();
    public DbSet<DemoSale> Sales => Set<DemoSale>();
    public DbSet<DemoStock> Stock => Set<DemoStock>();
    public DbSet<DemoReservation> Reservations => Set<DemoReservation>();
    public DbSet<DemoPayment> Payments => Set<DemoPayment>();
    public DbSet<DemoBusinessEntry> Entries => Set<DemoBusinessEntry>();
    public DbSet<DemoEmail> Emails => Set<DemoEmail>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("Demo");
        foreach (var entity in new[] { typeof(DemoOperation), typeof(DemoSale), typeof(DemoStock), typeof(DemoReservation),
            typeof(DemoPayment), typeof(DemoBusinessEntry), typeof(DemoEmail) })
        {
            var builder = model.Entity(entity);
            builder.HasKey("Id");
            builder.Property<string>("Id").HasMaxLength(200);
        }
        model.Entity<DemoOperation>().Property(x => x.RequestId).HasMaxLength(96);
        model.Entity<DemoOperation>().Property(x => x.TestRunId).HasMaxLength(96);
        model.Entity<DemoOperation>().HasIndex(x => new { x.TestRunId, x.RequestId });
        model.Entity<DemoOperation>().Property(x => x.InputHash).HasMaxLength(64);
        model.Entity<DemoSale>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<DemoReservation>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<DemoPayment>().Property(x => x.Version).IsConcurrencyToken();
        foreach (var property in model.Model.GetEntityTypes().SelectMany(x => x.GetProperties()).Where(x => x.ClrType == typeof(decimal)))
        { property.SetPrecision(18); property.SetScale(2); }
        model.UseMuleModel();
    }
}

