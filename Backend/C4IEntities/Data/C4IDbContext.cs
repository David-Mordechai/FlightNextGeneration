using C4IEntities.Models;
using Microsoft.EntityFrameworkCore;

namespace C4IEntities.Data;

public class C4IDbContext : DbContext
{
    public C4IDbContext(DbContextOptions<C4IDbContext> options) : base(options)
    {
    }

    public DbSet<NoFlyZone> NoFlyZones { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.Entity<NoFlyZone>()
            .Property(x => x.Geometry)
            .HasColumnType("geometry(Polygon, 4326)"); // EPSG:4326 for GPS coordinates
    }
}
