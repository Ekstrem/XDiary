using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppleHealthGateway.Storage;

internal class DomainEventMapping : IEntityTypeConfiguration<DomainEventEventEntry>
{
    public void Configure(EntityTypeBuilder<DomainEventEventEntry> builder)
    {
        builder.ToTable("DomainEvents");

        builder.HasKey(x => new { x.Id, x.Version });

        builder.Property(x => x.CorrelationToken).IsRequired();
        builder.Property(x => x.CommandName).HasMaxLength(256);
        builder.Property(x => x.SubjectName).HasMaxLength(256);
        builder.Property(x => x.ChangedValueObjectsJson).HasColumnType("text");
        builder.Property(x => x.BoundedContext).HasMaxLength(128);
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
