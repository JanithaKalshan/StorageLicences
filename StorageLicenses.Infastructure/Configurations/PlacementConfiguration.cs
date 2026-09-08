using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorageLicenses.Domain.Entities;

namespace StorageLicenses.Infastructure.Configurations;

public class PlacementConfiguration : IEntityTypeConfiguration<Placement>
{
    public void Configure(EntityTypeBuilder<Placement> builder)
    {
        builder.ToTable("Placements");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).ValueGeneratedOnAdd();

        builder.Property(p => p.UnitId).IsRequired();

        builder.Property(p => p.ItemId).IsRequired();

        builder.Property(p => p.PlacementClass)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.ScheduledDate).IsRequired();

        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(p => p.Item)
            .WithMany()
            .HasForeignKey(p => p.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // Used by capacity and pallet-spacing rule queries: all active placements for a unit,
        // filtered by class and ordered by scheduled date.
        builder.HasIndex(p => new { p.UnitId, p.PlacementClass, p.Status, p.ScheduledDate });
    }
}
