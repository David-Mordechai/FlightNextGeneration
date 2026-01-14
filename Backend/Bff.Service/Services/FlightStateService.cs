namespace Bff.Service.Services;

public enum FlightMode
{
    Orbiting,
    Transiting
}

public class FlightStateService
{
    // Current UAV Position
    public double CurrentLat { get; private set; } = 31.801447;
    public double CurrentLng { get; private set; } = 34.643497;
    
    // Target Orbit Center
    public double TargetLat { get; private set; } = 31.801447;
    public double TargetLng { get; private set; } = 34.643497;

    private FlightMode Mode { get; set; } = FlightMode.Orbiting;
    
    // Navigation Queues
    private Queue<(double Lat, double Lng)> Waypoints { get; set; } = new();
    private List<(double Lat, double Lng)>? PendingPath { get; set; }

    private double OrbitRadius { get; set; } = 0.01; // ~1km
    private double OrbitAngle { get; set; }

    // Target Telemetry
    private double TargetSpeedKts { get; set; } = 105;
    private double TargetAltitudeFt { get; set; } = 4000;

    // Current internal telemetry (for smoothing)
    public double CurrentSpeedKts { get; private set; } = 105;
    public double CurrentAltitudeFt { get; private set; } = 4000;

    public object CurrentState => new 
    { 
        Lat = CurrentLat, 
        Lng = CurrentLng,
        Heading = GetHeading(),
        Altitude = CurrentAltitudeFt,
        Speed = CurrentSpeedKts,
        TargetLat,
        TargetLng
    };

    // Physics Constants
    private const double BaseStepPerKnotTick = 0.000025 / 105.0; // Normalized step per knot (assuming 20Hz)

    public void UpdatePhysics()
    {
        // 1. Smoothly adjust Speed towards Target (Rate: ~2 kts/sec @ 20Hz)
        var speedDelta = TargetSpeedKts - CurrentSpeedKts;
        if (Math.Abs(speedDelta) > 0.1)
            CurrentSpeedKts += Math.Sign(speedDelta) * 0.1;

        // 2. Smoothly adjust Altitude towards Target (Rate: ~10 ft/sec @ 20Hz)
        var altDelta = TargetAltitudeFt - CurrentAltitudeFt;
        if (Math.Abs(altDelta) > 1.0)
            CurrentAltitudeFt += Math.Sign(altDelta) * 0.5;

        // 3. Calculate movement step based on current speed
        var currentStep = CurrentSpeedKts * BaseStepPerKnotTick;

        if (Mode == FlightMode.Transiting)
            UpdateTransit(currentStep);
        else if (Mode == FlightMode.Orbiting) UpdateOrbit(currentStep);
    }

    private void UpdateTransit(double step)
    {
        var dLat = TargetLat - CurrentLat;
        var dLng = TargetLng - CurrentLng;
        var distance = Math.Sqrt(dLat * dLat + dLng * dLng);

        // Arrival threshold (approx 100m, 0.001 deg)
        if (distance < 0.001)
        {
            if (Waypoints.Count > 0)
            {
                // Proceed to next waypoint
                var next = Waypoints.Dequeue();
                TargetLat = next.Lat;
                TargetLng = next.Lng;
            }
            else
            {
                // Reached final destination
                Mode = FlightMode.Orbiting;
                OrbitAngle = Math.Atan2(CurrentLng - TargetLng, CurrentLat - TargetLat);
            }
        }
        else
        {
            var ratio = step / distance;
            CurrentLat += dLat * ratio;
            CurrentLng += dLng * ratio;
        }
    }

    private void UpdateOrbit(double step)
    {
        // Angular velocity: omega = v / r
        var angleStep = step / OrbitRadius;
        OrbitAngle += angleStep;
        if (OrbitAngle > Math.PI * 2) OrbitAngle -= Math.PI * 2;

        CurrentLat = TargetLat + OrbitRadius * Math.Cos(OrbitAngle);
        CurrentLng = TargetLng + OrbitRadius * Math.Sin(OrbitAngle);
    }

    public double GetHeading()
    {
        // Simple approximation or stored heading
        // For orbit: Tangent to circle. For transit: Vector to target.
        if (Mode == FlightMode.Transiting)
        {
             return Math.Atan2(TargetLng - CurrentLng, TargetLat - CurrentLat) * (180 / Math.PI);
        }

        // Heading is tangent to the circle (OrbitAngle + 90 degrees)
        // But we need to be careful with coordinate system. 
        // Lat/Lng is Y/X. Atan2(y, x).
        // Let's rely on the previous frame diff for simplicity in the worker, 
        // or calculate it analytically here.
        // Analytic: Tangent angle = OrbitAngle + PI/2 (counter-clockwise)
        return OrbitAngle * (180 / Math.PI) + 90;
    }

    public void SetNewDestination(double lat, double lng)
    {
        Waypoints.Clear();
        TargetLat = lat;
        TargetLng = lng;
        Mode = FlightMode.Transiting;
    }

    public void SetPendingPath(List<(double Lat, double Lng)> path)
    {
        PendingPath = path;
    }

    public bool ExecutePendingPath()
    {
        if (PendingPath == null || PendingPath.Count == 0) return false;

        Waypoints.Clear();
        foreach (var point in PendingPath)
        {
            Waypoints.Enqueue(point);
        }

        // Start flying to the first point immediately
        if (Waypoints.Count > 0)
        {
            var first = Waypoints.Dequeue();
            TargetLat = first.Lat;
            TargetLng = first.Lng;
            Mode = FlightMode.Transiting;
        }

        PendingPath = null; // Clear after execution
        return true;
    }

    public void SetSpeed(double speedKts)
    {
        TargetSpeedKts = speedKts;
    }

    public void SetAltitude(double altitudeFt)
    {
        TargetAltitudeFt = altitudeFt;
    }
}
