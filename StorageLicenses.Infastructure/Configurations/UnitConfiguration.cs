using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorageLicenses.Domain.Entities;

namespace StorageLicenses.Infastructure.Configurations;

public class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("Units");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).ValueGeneratedOnAdd();

        builder.Property(u => u.PalletCapacity).IsRequired();

        builder.Property(u => u.BoxCapacity).IsRequired();

        builder.HasMany(u => u.Licences)
            .WithOne(l => l.Unit)
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.Placements)
            .WithOne(p => p.Unit)
            .HasForeignKey(p => p.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Metadata.FindNavigation(nameof(Unit.Licences))!.SetPropertyAccessMode(Microsoft.EntityFrameworkCore.PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Unit.Placements))!.SetPropertyAccessMode(Microsoft.EntityFrameworkCore.PropertyAccessMode.Field);
    }
}
