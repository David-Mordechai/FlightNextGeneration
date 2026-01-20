namespace C4IEntities.Models;

public class RouteRequest
{
    public double StartLat { get; set; }
    public double StartLng { get; set; }
    public double EndLat { get; set; }
    public double EndLng { get; set; }
    public double AltitudeFt { get; set; }
}

public class RouteResponse
{
    public required List<GeoPoint> Path { get; set; }
    public double TotalDistanceMeters { get; set; }
}

public class GeoPoint
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public double AltitudeFt { get; set; }
}
