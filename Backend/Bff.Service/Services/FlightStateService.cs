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
    
    // Navigation Queues (3D)
    private Queue<(double Lat, double Lng, double Alt)> Waypoints { get; set; } = new();
    private List<(double Lat, double Lng, double Alt)>? PendingPath { get; set; }

    private double TargetOrbitRadius { get; set; } = 0.027; // ~3km target radius
    private double OrbitAngle { get; set; }

    // Target Telemetry
    private double TargetSpeedKts { get; set; } = 105;
    private double TargetAltitudeFt { get; set; } = 4000;

    // Payload (Camera Gimbal) State
    public double PayloadPitch { get; private set; } = -45; // Degrees (down)
    public double PayloadYaw { get; private set; } = 0;    // Degrees (relative to North)
    private (double Lat, double Lng)? PayloadLockLocation { get; set; }

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
        TargetLng,
        PayloadPitch,
        PayloadYaw,
        Mode = Mode.ToString() // Send Mode as string ('Transiting' or 'Orbiting')
    };

    // Physics Constants
    private const double BaseStepPerKnotTick = 0.000025 / 105.0; // Normalized step per knot (assuming 20Hz)
    private const double FEET_TO_METERS = 0.3048;

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

        // 4. Update Payload Gimbal (After position update for sync)
        UpdatePayload();
    }

    private void UpdatePayload()
    {
        double? tLat = PayloadLockLocation?.Lat;
        double? tLng = PayloadLockLocation?.Lng;

        // Fallback to navigation target if transiting and no manual lock is set
        if (!tLat.HasValue && Mode == FlightMode.Transiting)
        {
            tLat = TargetLat;
            tLng = TargetLng;
        }

        if (tLat.HasValue && tLng.HasValue)
        {
            // Calculate Yaw (Bearing from UAV to Target)
            var dLng = (tLng.Value - CurrentLng) * Math.Cos(CurrentLat * Math.PI / 180);
            var dLat = tLat.Value - CurrentLat;
            PayloadYaw = Math.Atan2(dLng, dLat) * 180 / Math.PI;

            // Calculate Pitch (Angle down to target)
            var horizontalDistMeters = Math.Sqrt(dLng * dLng + dLat * dLat) * 111320; // Approx meters
            var altMeters = CurrentAltitudeFt * FEET_TO_METERS;
            
            // Avoid division by zero and handle very close targets
            if (horizontalDistMeters < 10) horizontalDistMeters = 10;
            PayloadPitch = -Math.Atan2(altMeters, horizontalDistMeters) * 180 / Math.PI;
        }
        else if (Mode == FlightMode.Orbiting && !PayloadLockLocation.HasValue)
        {
            // Default: Look forward and slightly down while orbiting
            PayloadYaw = GetHeading();
            PayloadPitch = -45;
        }
    }

    public void PointPayload(double lat, double lng)
    {
        PayloadLockLocation = (lat, lng);
    }

    public void ResetPayload()
    {
        PayloadLockLocation = null;
        PayloadPitch = -45;
        PayloadYaw = 0;
    }

    private void UpdateTransit(double step)
    {
        var dLat = TargetLat - CurrentLat;
        var dLng = TargetLng - CurrentLng;
        var distance = Math.Sqrt(dLat * dLat + dLng * dLng);

        // Orbit Capture: If we are on the final leg and hit the orbit perimeter (1km), start orbiting.
        // This prevents flying to the center and "jumping" or spiraling out.
        if (Waypoints.Count == 0 && distance <= TargetOrbitRadius)
        {
            Mode = FlightMode.Orbiting;
            // Calculate angle from Center TO Current Position (Perimeter Intercept)
            OrbitAngle = Math.Atan2(CurrentLng - TargetLng, CurrentLat - TargetLat);
            return;
        }

        // Standard Waypoint Arrival (approx 11m threshold)
        if (distance < 0.0001)
        {
            // Snap to the exact target to prevent drift accumulation
            CurrentLat = TargetLat;
            CurrentLng = TargetLng;

            if (Waypoints.Count > 0)
            {
                // Proceed to next waypoint
                var next = Waypoints.Dequeue();
                TargetLat = next.Lat;
                TargetLng = next.Lng;
                TargetAltitudeFt = next.Alt; // Update altitude for this leg
            }
            // Note: The 'else' block for final destination is handled by Orbit Capture above.
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
        var angleStep = step / TargetOrbitRadius;
        OrbitAngle += angleStep;
        if (OrbitAngle > Math.PI * 2) OrbitAngle -= Math.PI * 2;

        CurrentLat = TargetLat + TargetOrbitRadius * Math.Cos(OrbitAngle);
        CurrentLng = TargetLng + TargetOrbitRadius * Math.Sin(OrbitAngle);
    }

    public double GetHeading()
    {
        // Simple approximation or stored heading
        if (Mode == FlightMode.Transiting)
        {
             return Math.Atan2(TargetLng - CurrentLng, TargetLat - CurrentLat) * (180 / Math.PI);
        }

        // Tangent angle = OrbitAngle + PI/2 (counter-clockwise)
        return OrbitAngle * (180 / Math.PI) + 90;
    }

    public void SetNewDestination(double lat, double lng)
    {
        Waypoints.Clear();
        TargetLat = lat;
        TargetLng = lng;
        // Keep current TargetAltitudeFt
        Mode = FlightMode.Transiting;
    }

    public void SetPendingPath(List<(double Lat, double Lng, double Alt)> path)
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
            TargetAltitudeFt = first.Alt;
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
