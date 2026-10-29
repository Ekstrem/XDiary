using Privacy.InternalContracts;
using Microsoft.EntityFrameworkCore;

namespace Privacy.Storage.ReadModel;

/// <summary>
/// DbContext для Read-моделей (проекций) приватности.
/// Хранит проекцию `pin_credentials` (схема public — профиль Single).
/// </summary>
public class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options)
    {
    }

    public DbSet<PrivacyReadModel> PinCredentials { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PrivacyReadModel>(entity =>
        {
            entity.ToTable("pin_credentials");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Salt).HasMaxLength(256);
            entity.Property(e => e.Hash).HasMaxLength(256);
            entity.Property(e => e.Iterations).IsRequired();

            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.UserId).IsUnique();
        });
    }
}
