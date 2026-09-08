using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorageLicenses.Domain.Entities;

namespace StorageLicenses.Infastructure.Configurations;

public class StorageLicenceConfiguration : IEntityTypeConfiguration<StorageLicence>
{
    public void Configure(EntityTypeBuilder<StorageLicence> builder)
    {
        builder.ToTable("StorageLicences");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).ValueGeneratedOnAdd();

        builder.Property(l => l.UnitId).IsRequired();

        builder.Property(l => l.HolderId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(l => l.GrantDate).IsRequired();

        builder.Property(l => l.TermYears).IsRequired();

        builder.Property(l => l.SurrenderedDate).IsRequired(false);

        // A unit must never have more than one licence in force at a time; enforced in the
        // domain layer, not as a database constraint.
        builder.HasIndex(l => new { l.UnitId, l.GrantDate });

        builder.HasIndex(l => l.HolderId);
    }
}
