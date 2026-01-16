using System.ComponentModel.DataAnnotations;
using NtsPoint = NetTopologySuite.Geometries.Point;

namespace C4IEntities.Models;

public enum PointType
{
    Home,
    Target
}

public class Point
{
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public NtsPoint Location { get; set; } = NtsPoint.Empty;

    public PointType Type { get; set; }
}
