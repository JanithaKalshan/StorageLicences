using Microsoft.EntityFrameworkCore;
using StorageLicenses.Domain.Entities;

namespace StorageLicenses.Infastructure.Repositories;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<StorageLicence> StorageLicences => Set<StorageLicence>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<Placement> Placements => Set<Placement>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);


        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
