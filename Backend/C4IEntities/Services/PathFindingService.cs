using C4IEntities.Data;
using C4IEntities.Models;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace C4IEntities.Services;

public class PathFindingService(C4IDbContext context)
{
    private readonly GeometryFactory _geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public async Task<RouteResponse> CalculateOptimalPath(RouteRequest request)
    {
        // 1. Fetch Active Zones
        var zones = await context.NoFlyZones
            .Where(z => z.IsActive)
            .ToListAsync();

        var startPoint = _geometryFactory.CreatePoint(new Coordinate(request.StartLng, request.StartLat));
        var endPoint = _geometryFactory.CreatePoint(new Coordinate(request.EndLng, request.EndLat));

        // 2. Build Nodes (Start, End, + Zone Vertices)
        // Buffer zones to create a safety margin. 
        // Increased buffer to 0.0005 (~55m) to prevent UAV from flying too close to the edge.
        // quadrantSegments: 2 reduces vertex count (chamfered corners) vs default 8, improving performance.
        var obstacles = zones
            .Select(z => z.Geometry.Buffer(0.0005, 2) as Polygon)
            .Where(p => p != null)
            .Cast<Polygon>()
            .ToList();
        
        // Check if direct path is clear first (Optimization)
        var directLine = _geometryFactory.CreateLineString([startPoint.Coordinate, endPoint.Coordinate]);
        if (IsPathClear(directLine, obstacles))
        {
            return new RouteResponse
            {
                Path =
                [
                    new GeoPoint { Lat = request.StartLat, Lng = request.StartLng },
                    new GeoPoint { Lat = request.EndLat, Lng = request.EndLng }
                ],
                TotalDistanceMeters = HaversineDistance(startPoint.Coordinate, endPoint.Coordinate)
            };
        }

        // 3. Build Visibility Graph
        // Collect all potential nodes: Start, End, and all obstacle vertices
        var nodes = new List<Coordinate> { startPoint.Coordinate, endPoint.Coordinate };
        foreach (var obstacle in obstacles)
        {
            // ExteriorRing coordinates include the closing point (same as first), so take all but last
            nodes.AddRange(obstacle.ExteriorRing.Coordinates.Take(obstacle.ExteriorRing.Coordinates.Length - 1));
        }

        // Build Adjacency List
        var graph = new Dictionary<Coordinate, List<Coordinate>>();
        
        // Initialize graph keys
        foreach (var node in nodes)
        {
            if (!graph.ContainsKey(node)) graph[node] = new List<Coordinate>();
        }

        // Connect visible nodes
        // O(N^2) complexity. For very large N, a spatial index or specialized visibility algorithm is needed.
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                var u = nodes[i];
                var v = nodes[j];
                
                // Optimization: Don't check if nodes are too far apart? (optional)
                
                var segment = _geometryFactory.CreateLineString(new[] { u, v });
                
                // If segment does not intersect any obstacle interior
                if (IsPathClear(segment, obstacles))
                {
                    graph[u].Add(v);
                    graph[v].Add(u);
                }
            }
        }

        // 4. Run Dijkstra
        var pathCoords = Dijkstra(graph, startPoint.Coordinate, endPoint.Coordinate);
        
        if (pathCoords.Count == 0)
        {
             // Fallback or empty if no path found
             return new RouteResponse
             {
                 Path = new List<GeoPoint>(),
                 TotalDistanceMeters = 0
             };
        }

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
            // Fast envelope check first
            if (!obstacle.EnvelopeInternal.Intersects(segment.EnvelopeInternal)) continue;

            // Robust topological check using DE-9IM matrix.
            // We must avoid the segment passing through the INTERIOR of the obstacle.
            // It IS allowed to touch or run along the BOUNDARY.
            
            var matrix = obstacle.Relate(segment);

            // Check if Interior(obstacle) intersects Interior(segment)
            // The value at [Location.Interior, Location.Interior] should be 'F' (False) or -1.
            // If it's 0, 1, or 2 (Dimension), then they intersect.
            if (matrix[Location.Interior, Location.Interior] != Dimension.False) return false;

            // Check if Interior(obstacle) intersects Boundary(segment)
            // This prevents endpoints from being strictly inside the obstacle.
            if (matrix[Location.Interior, Location.Boundary] != Dimension.False) return false;
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
            if (!queue.TryDequeue(out var u, out var currentDist)) break;

            if (currentDist > distances[u]) continue; // Stale node
            if (u.Equals(end)) break;

            if (graph.TryGetValue(u, out var neighbors))
            {
                foreach (var v in neighbors)
                {
                    var weight = HaversineDistance(u, v);
                    var alt = distances[u] + weight;
                    if (alt < distances[v])
                    {
                        distances[v] = alt;
                        previous[v] = u;
                        queue.Enqueue(v, alt);
                    }
                }
            }
        }

        // Reconstruct path
        var path = new List<Coordinate>();
        var current = end;
        
        if (!distances.ContainsKey(end) || distances[end] == double.MaxValue) 
            return path; // No path found

        while (!current.Equals(start))
        {
            path.Add(current);
            if (!previous.ContainsKey(current)) return new List<Coordinate>(); // Should not happen if path exists
            current = previous[current];
        }
        path.Add(start);
        path.Reverse();
        return path;
    }

    private static double HaversineDistance(Coordinate c1, Coordinate c2)
    {
        const double R = 6371000; // Radius of Earth in meters
        var lat1 = c1.Y * Math.PI / 180;
        var lat2 = c2.Y * Math.PI / 180;
        var dLat = (c2.Y - c1.Y) * Math.PI / 180;
        var dLon = (c2.X - c1.X) * Math.PI / 180;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }

    private double CalculateTotalDistance(List<Coordinate> path)
    {
        double dist = 0;
        for (int i = 0; i < path.Count - 1; i++)
        {
            dist += HaversineDistance(path[i], path[i+1]);
        }
        return dist;
    }
}
