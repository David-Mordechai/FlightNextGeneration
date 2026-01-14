using C4IEntities.Data;
using C4IEntities.Models;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace C4IEntities.Services;

public class PathFindingService
{
    private readonly C4IDbContext _context;
    private readonly GeometryFactory _geometryFactory;

    public PathFindingService(C4IDbContext context)
    {
        _context = context;
        _geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
    }

    public async Task<RouteResponse> CalculateOptimalPath(RouteRequest request)
    {
        // 1. Fetch Active Zones
        var zones = await _context.NoFlyZones
            .Where(z => z.IsActive && z.Geometry != null)
            .ToListAsync();

        var startPoint = _geometryFactory.CreatePoint(new Coordinate(request.StartLng, request.StartLat));
        var endPoint = _geometryFactory.CreatePoint(new Coordinate(request.EndLng, request.EndLat));

        // 2. Build Nodes (Start, End, + Zone Vertices)
        // We buffer zones slightly to avoid grazing the edge
        var obstacles = zones.Select(z => z.Geometry.Buffer(0.0001) as Polygon).Where(p => p != null).Cast<Polygon>().ToList();
        
        // Simple check: Is direct path clear?
        var directLine = _geometryFactory.CreateLineString(new[] { startPoint.Coordinate, endPoint.Coordinate });
        if (IsPathClear(directLine, obstacles))
        {
            return new RouteResponse
            {
                Path = new List<GeoPoint> 
                { 
                    new() { Lat = request.StartLat, Lng = request.StartLng },
                    new() { Lat = request.EndLat, Lng = request.EndLng }
                },
                TotalDistanceMeters = GetDistance(startPoint.Coordinate, endPoint.Coordinate)
            };
        }

        // 3. Build Visibility Graph
        // Collect all potential nodes
        var nodes = new List<Coordinate> { startPoint.Coordinate, endPoint.Coordinate };
        foreach (var obstacle in obstacles)
        {
            // Add obstacle vertices
            nodes.AddRange(obstacle.ExteriorRing.Coordinates.Take(obstacle.ExteriorRing.Coordinates.Length - 1)); // -1 to avoid duplicate close ring
        }

        // Build Adjacency List
        var graph = new Dictionary<Coordinate, List<Coordinate>>();
        
        // This is O(N^2) checking - okay for small number of zones (< 100 vertices)
        // For larger sets, spatial indexing (STRtree) would be needed.
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                var u = nodes[i];
                var v = nodes[j];
                
                var segment = _geometryFactory.CreateLineString(new[] { u, v });
                if (IsPathClear(segment, obstacles))
                {
                    if (!graph.ContainsKey(u)) graph[u] = new List<Coordinate>();
                    if (!graph.ContainsKey(v)) graph[v] = new List<Coordinate>();
                    
                    graph[u].Add(v);
                    graph[v].Add(u);
                }
            }
        }

        // 4. Run Dijkstra
        var pathCoords = Dijkstra(graph, startPoint.Coordinate, endPoint.Coordinate);
        
        return new RouteResponse
        {
            Path = pathCoords.Select(c => new GeoPoint { Lat = c.Y, Lng = c.X }).ToList(),
            TotalDistanceMeters = CalculateTotalDistance(pathCoords)
        };
    }

    private bool IsPathClear(LineString segment, List<Polygon> obstacles)
    {
        foreach (var obstacle in obstacles)
        {
            if (obstacle.Intersects(segment))
            {
                // If it just touches the boundary, it's technically okay in a visibility graph, 
                // but intersects returns true for boundary too.
                // We buffered obstacles earlier, so strict intersection check is safer.
                
                // Allow if the intersection is just the endpoints (which are vertices)
                var intersection = obstacle.Intersection(segment);
                if (intersection is Point || intersection is MultiPoint)
                {
                    // Check if points match segment endpoints
                    // For simplicity in this v1, assume ANY intersection is bad unless we implement robust "touches" check.
                    // But visibility graph relies on grazing edges.
                    
                    // Since we buffered obstacles outward, touching the buffer is "too close". 
                    // EXCEPT that nodes ARE on the buffer boundary! 
                    // So a segment between two nodes on the same obstacle will intersect the boundary.
                    
                    // Improvement: Use 'Crosses' or 'Overlaps' or check if interior intersects.
                    if (obstacle.Covers(segment)) return false; // Segment inside
                    
                    // If it crosses the boundary (enters -> exits), it's bad.
                    if (segment.Crosses(obstacle)) return false; 
                    
                    // If segment is completely within the obstacle (interior)
                    if (obstacle.Contains(segment)) return false;
                }
                else
                {
                    return false; // LineString intersection = bad
                }
            }
        }
        return true;
    }

    private List<Coordinate> Dijkstra(Dictionary<Coordinate, List<Coordinate>> graph, Coordinate start, Coordinate end)
    {
        var distances = new Dictionary<Coordinate, double>();
        var previous = new Dictionary<Coordinate, Coordinate>();
        var queue = new PriorityQueue<Coordinate, double>();

        foreach (var node in graph.Keys)
        {
            distances[node] = double.MaxValue;
        }
        distances[start] = 0;
        queue.Enqueue(start, 0);

        while (queue.Count > 0)
        {
            var u = queue.Dequeue();

            if (u.Equals(end)) break;

            if (graph.TryGetValue(u, out var neighbors))
            {
                foreach (var v in neighbors)
                {
                    var alt = distances[u] + u.Distance(v);
                    if (alt < distances[v])
                    {
                        distances[v] = alt;
                        previous[v] = u;
                        queue.Enqueue(v, alt);
                    }
                }
            }
        }

        var path = new List<Coordinate>();
        var current = end;
        while (!current.Equals(start))
        {
            path.Add(current);
            if (!previous.ContainsKey(current)) return new List<Coordinate>(); // No path
            current = previous[current];
        }
        path.Add(start);
        path.Reverse();
        return path;
    }

    private double GetDistance(Coordinate c1, Coordinate c2)
    {
        // Simple Euclidean for now (degrees), acceptable for small areas. 
        // For real world meters, use a Haversine or NTS PROJ.
        // Returning rough approximation in meters (1 deg lat ~= 111km)
        return c1.Distance(c2) * 111000;
    }

    private double CalculateTotalDistance(List<Coordinate> path)
    {
        double dist = 0;
        for (int i = 0; i < path.Count - 1; i++)
        {
            dist += GetDistance(path[i], path[i+1]);
        }
        return dist;
    }
}
