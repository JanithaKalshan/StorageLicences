using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorageLicenses.Domain.Entities;

namespace StorageLicenses.Infastructure.Configurations;

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("Items");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).ValueGeneratedOnAdd();

        builder.Property(i => i.Reference)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(i => i.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(i => i.IntakeDate).IsRequired(false);

        builder.HasIndex(i => i.Reference).IsUnique();
    }
}
