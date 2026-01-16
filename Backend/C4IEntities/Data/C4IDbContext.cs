using C4IEntities.Models;
using Microsoft.EntityFrameworkCore;

namespace C4IEntities.Data;

public class C4IDbContext(DbContextOptions<C4IDbContext> options) : DbContext(options)
{
    public DbSet<NoFlyZone> NoFlyZones { get; set; }
    public DbSet<Point> Points { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.Entity<NoFlyZone>()
            .Property(x => x.Geometry)
            .HasColumnType("geometry(Polygon, 4326)"); // EPSG:4326 for GPS coordinates

        modelBuilder.Entity<Point>()
            .Property(x => x.Location)
            .HasColumnType("geometry(Point, 4326)");
    }
}
