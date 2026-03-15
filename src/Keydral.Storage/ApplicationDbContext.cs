using Microsoft.EntityFrameworkCore;
using Keydral.Storage.Entities;

namespace Keydral.Storage;

/// <summary>
/// Entity Framework Core DbContext for Keydral.
/// Configures all entities, relationships, and database mappings.
/// </summary>
public class ApplicationDbContext : DbContext
{
    /// <summary>
    /// Constructor accepting DbContextOptions.
    /// </summary>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    // DbSets for each entity
    public DbSet<Secret> Secrets => Set<Secret>();
    public DbSet<SecretVersion> SecretVersions => Set<SecretVersion>();
    public DbSet<EncryptionKey> EncryptionKeys => Set<EncryptionKey>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>
    /// Configures the entity model, relationships, and constraints.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Secret entity
        modelBuilder.Entity<Secret>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.EncryptedValue).IsRequired();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            // Foreign key to EncryptionKey
            entity.HasOne(e => e.EncryptionKey)
                .WithMany(k => k.Secrets)
                .HasForeignKey(e => e.EncryptionKeyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationships
            entity.HasMany(e => e.Versions)
                .WithOne(v => v.Secret)
                .HasForeignKey(v => v.SecretId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.AuditLogs)
                .WithOne(a => a.Secret)
                .HasForeignKey(a => a.SecretId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for common queries
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.UpdatedAt);
            entity.HasIndex(e => e.IsDeleted);
        });

        // Configure SecretVersion entity
        modelBuilder.Entity<SecretVersion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EncryptedValue).IsRequired();
            entity.HasIndex(e => new { e.SecretId, e.VersionNumber }).IsUnique();

            // Foreign key to EncryptionKey
            entity.HasOne(e => e.EncryptionKey)
                .WithMany(k => k.Versions)
                .HasForeignKey(e => e.EncryptionKeyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            entity.HasIndex(e => e.SecretId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure EncryptionKey entity
        modelBuilder.Entity<EncryptionKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.KeyId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.EncryptedDataKey).IsRequired();
            entity.Property(e => e.Algorithm).IsRequired().HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            // Indexes
            entity.HasIndex(e => e.KeyId).IsUnique();
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure Policy entity
        modelBuilder.Entity<Policy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Principal).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ResourcePattern).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.Effect).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Actions).IsRequired();
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            // Indexes for policy lookups
            entity.HasIndex(e => e.Principal);
            entity.HasIndex(e => e.ResourcePattern);
            entity.HasIndex(e => e.IsEnabled);
            entity.HasIndex(e => e.IsDeleted);
        });

        // Configure AuditLog entity
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ResourceType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ResourceId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Actor).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Result).IsRequired().HasMaxLength(20);

            // Foreign key to Secret (optional)
            entity.HasOne(e => e.Secret)
                .WithMany(s => s.AuditLogs)
                .HasForeignKey(e => e.SecretId)
                .OnDelete(DeleteBehavior.SetNull);

            // Append-only table: no updates, only inserts
            // Indexes for query performance on audit logs
            entity.HasIndex(e => e.Timestamp).IsDescending();
            entity.HasIndex(e => e.Actor);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.ResourceId);
            entity.HasIndex(e => e.Result);
            entity.HasIndex(e => new { e.Timestamp, e.Result });
        });

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.KeycloakId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.KeycloakId).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            // Indexes for user lookups
            entity.HasIndex(e => e.LastActivityAt);
            entity.HasIndex(e => e.IsActive);
        });

        // Configure Role entity
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.IsSystemRole).HasDefaultValue(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            // Indexes
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.IsSystemRole);
        });

        // Seed default roles
        var secretReaderRole = new Role
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "secret-reader",
            Description = "Can read secret values and versions",
            Permissions = "secrets:read,audit:read",
            IsSystemRole = true,
            IsActive = true,
            CreatedAt = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = "system"
        };

        var secretWriterRole = new Role
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "secret-writer",
            Description = "Can read, write, and delete secrets",
            Permissions = "secrets:read,secrets:write,secrets:delete,audit:read",
            IsSystemRole = true,
            IsActive = true,
            CreatedAt = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = "system"
        };

        var secretAdminRole = new Role
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "secret-admin",
            Description = "Full administrator access to secrets, policies, and audit",
            Permissions = "secrets:*,policies:*,users:*,roles:*,audit:*,encryption:*",
            IsSystemRole = true,
            IsActive = true,
            CreatedAt = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = "system"
        };

        modelBuilder.Entity<Role>().HasData(secretReaderRole, secretWriterRole, secretAdminRole);
    }
}
