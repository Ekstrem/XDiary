using Partners.InternalContracts;
using Microsoft.EntityFrameworkCore;

namespace Partners.Storage.ReadModel;

/// <summary>
/// DbContext для Read-моделей (проекций) партнёров.
/// Хранит проекцию `partners` (схема public — профиль Single).
/// </summary>
public class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options) : base(options)
    {
    }

    public DbSet<PartnersReadModel> Partners { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PartnersReadModel>(entity =>
        {
            entity.ToTable("partners");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Photo).HasColumnType("text");
            entity.Property(e => e.Gender).HasMaxLength(16);
            entity.Property(e => e.Notes).HasColumnType("text");

            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.Name);
        });
    }
}
