using Krackend.Security.Core;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Security.Storage.EntityFramework;

/// <summary>
/// Entity Framework database context for Krackend product authorization data.
/// </summary>
public sealed class KrackendSecurityDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KrackendSecurityDbContext"/> class.
    /// </summary>
    /// <param name="options">Database context options.</param>
    public KrackendSecurityDbContext(DbContextOptions<KrackendSecurityDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets the known external subjects.
    /// </summary>
    public DbSet<KrackendSubject> Subjects => Set<KrackendSubject>();

    /// <summary>
    /// Gets direct role assignments.
    /// </summary>
    public DbSet<KrackendRoleAssignment> RoleAssignments => Set<KrackendRoleAssignment>();

    /// <summary>
    /// Gets direct permission assignments.
    /// </summary>
    public DbSet<KrackendPermissionAssignment> PermissionAssignments => Set<KrackendPermissionAssignment>();

    /// <summary>
    /// Gets external group role assignments.
    /// </summary>
    public DbSet<KrackendExternalGroupRoleAssignment> ExternalGroupRoleAssignments => Set<KrackendExternalGroupRoleAssignment>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<KrackendSubject>(builder =>
        {
            builder.ToTable("Subjects", "Security");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(64);
            builder.Property(x => x.Provider).HasMaxLength(128).IsRequired();
            builder.Property(x => x.SubjectId).HasMaxLength(256).IsRequired();
            builder.Property(x => x.DisplayName).HasMaxLength(256);
            builder.Property(x => x.Email).HasMaxLength(320);
            builder.HasIndex(x => new { x.Provider, x.SubjectId }).IsUnique();
        });

        modelBuilder.Entity<KrackendRoleAssignment>(builder =>
        {
            builder.ToTable("RoleAssignments", "Security");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(64);
            builder.Property(x => x.Provider).HasMaxLength(128).IsRequired();
            builder.Property(x => x.SubjectId).HasMaxLength(256).IsRequired();
            builder.Property(x => x.Role).HasMaxLength(128).IsRequired();
            builder.Property(x => x.ScopeType).HasMaxLength(128).IsRequired();
            builder.Property(x => x.ScopeId).HasMaxLength(256);
            builder.Property(x => x.Source).HasMaxLength(64).IsRequired();
            builder.HasIndex(x => new { x.Provider, x.SubjectId, x.Role, x.ScopeType, x.ScopeId }).IsUnique();
        });

        modelBuilder.Entity<KrackendPermissionAssignment>(builder =>
        {
            builder.ToTable("PermissionAssignments", "Security");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(64);
            builder.Property(x => x.Provider).HasMaxLength(128).IsRequired();
            builder.Property(x => x.SubjectId).HasMaxLength(256).IsRequired();
            builder.Property(x => x.Permission).HasMaxLength(256).IsRequired();
            builder.Property(x => x.ScopeType).HasMaxLength(128).IsRequired();
            builder.Property(x => x.ScopeId).HasMaxLength(256);
            builder.Property(x => x.Source).HasMaxLength(64).IsRequired();
            builder.HasIndex(x => new { x.Provider, x.SubjectId, x.Permission, x.ScopeType, x.ScopeId }).IsUnique();
        });

        modelBuilder.Entity<KrackendExternalGroupRoleAssignment>(builder =>
        {
            builder.ToTable("ExternalGroupRoleAssignments", "Security");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(64);
            builder.Property(x => x.Provider).HasMaxLength(128).IsRequired();
            builder.Property(x => x.ExternalGroupId).HasMaxLength(256).IsRequired();
            builder.Property(x => x.Role).HasMaxLength(128).IsRequired();
            builder.Property(x => x.ScopeType).HasMaxLength(128).IsRequired();
            builder.Property(x => x.ScopeId).HasMaxLength(256);
            builder.Property(x => x.Source).HasMaxLength(64).IsRequired();
            builder.HasIndex(x => new { x.Provider, x.ExternalGroupId, x.Role, x.ScopeType, x.ScopeId }).IsUnique();
        });
    }
}
