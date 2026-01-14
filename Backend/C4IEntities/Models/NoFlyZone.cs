using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations;

namespace C4IEntities.Models;

public class NoFlyZone
{
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public Polygon Geometry { get; set; } = Polygon.Empty;

    public double MinAltitude { get; set; }

    public double MaxAltitude { get; set; }

    public bool IsActive { get; set; }
}
