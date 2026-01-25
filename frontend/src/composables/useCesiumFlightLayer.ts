import { ref, type ShallowRef } from 'vue';
import * as Cesium from 'cesium';
import { signalRService } from '../services/SignalRService';
import { TacticalBeamMaterialProperty, registerCustomMaterials } from '../utils/CesiumAdvancedMaterials';
import { useScreenLabels } from './useScreenLabels';

export function useCesiumFlightVisualization(viewer: ShallowRef<Cesium.Viewer | null>) {
    
    // Use Shared Label System
    const { registerLabel } = useScreenLabels();

    const uavEntities = new Map<string, Cesium.Entity>();
    const projectedPathEntities = new Map<string, Cesium.Entity>();
    const flightTargets = new Map<string, { lat: number, lng: number }>(); 
    const optimalPathEntity = ref<Cesium.Entity | null>(null);
    const waypointMarkerEntities = new Map<string, Cesium.Entity>();
    
    const finalDestination = ref<{lat: number, lng: number} | null>(null);
    const flightPathCache = new Map<string, any[]>(); // Store current optimal path for the beam to follow
    
    const currentFlightData = ref<{
        flightId: string;
        lat: number;
        lng: number;
        altitude: number;
        speed: number;
        heading: number;
        payloadPitch: number;
        payloadYaw: number;
        flightMode: string;
    } | null>(null);

    // Reactive FOV state (Vertical Degrees) - Default to Narrow (5.0)
    const sensorFov = ref(5.0);

    const FEET_TO_METERS = 0.3048;

    const clearOptimalPath = () => {
        const currentViewer = viewer.value;
        if (!currentViewer) return;

        if (optimalPathEntity.value) {
            currentViewer.entities.remove(optimalPathEntity.value);
            optimalPathEntity.value = null;
        }
        
        waypointMarkerEntities.forEach(entity => currentViewer.entities.remove(entity));
        waypointMarkerEntities.clear();
        
        finalDestination.value = null;
        flightPathCache.clear();
    };

    const targetHeightCache = new Map<string, number>();

    const initializeFlightListeners = () => {
        const currentViewer = viewer.value;
        if (!currentViewer) return;

        // Register custom materials for this specific context
        registerCustomMaterials(currentViewer, 'main');

        // Listen for AI-driven Camera Focus
        signalRService.on('FocusCamera', (lat: number, lng: number) => {
            if (!currentViewer) return;
            
            currentViewer.camera.flyTo({
                destination: Cesium.Cartesian3.fromDegrees(
                    lng, 
                    lat - 0.05, // Offset latitude to look "forward"/North-ish
                    4000 // Altitude
                ),
                orientation: {
                    heading: Cesium.Math.toRadians(0), // North up
                    pitch: Cesium.Math.toRadians(-45), // 45 degree angle
                    roll: 0.0
                },
                duration: 3.0 // Smooth flight
            });
        });

        // Listen for Optimal Route
        signalRService.on('RouteCalculated', (pathData: any[]) => {
            if (!currentViewer) return;

            clearOptimalPath();
            
            // CRITICAL: Polylines need at least 2 points to render without crashing
            if (pathData && pathData.length >= 2) {
                // Cache the path for the beam logic
                flightPathCache.set('active', pathData);

                const lastPoint = pathData[pathData.length - 1];
                finalDestination.value = { lat: lastPoint.lat, lng: lastPoint.lng };

                // Cache height for the final destination to keep the beam stable
                const carto = Cesium.Cartographic.fromDegrees(lastPoint.lng, lastPoint.lat);
                Cesium.sampleTerrainMostDetailed(currentViewer.terrainProvider, [carto]).then(samples => {
                    if (samples && samples[0] && samples[0].height !== undefined) {
                        targetHeightCache.set('final', samples[0].height);
                    }
                });

                // Convert points to Cartesian3 (3D)
                // FIX: Lower the path by 200 meters to ensure it is always visually BELOW the UAV
                const positions = pathData.map(p => Cesium.Cartesian3.fromDegrees(p.lng, p.lat, ((p.altitudeFt || 0) * FEET_TO_METERS) - 200.0)); 

                optimalPathEntity.value = currentViewer.entities.add({
                    polyline: {
                        positions: positions,
                        width: 4,
                        material: new Cesium.ColorMaterialProperty(Cesium.Color.fromCssColorString('#10B981').withAlpha(0.8)),
                        // ATTACHMENT FIX: Push path into background
                        // @ts-ignore
                        eyeOffset: new Cesium.Cartesian3(0, 0, 500.0),
                        clampToGround: false 
                    }
                });

                // Add waypoint markers
                pathData.forEach((p, index) => {
                    const entity = currentViewer.entities.add({
                        // FIX: Lower markers by 200 meters to match the path
                        position: Cesium.Cartesian3.fromDegrees(p.lng, p.lat, ((p.altitudeFt || 0) * FEET_TO_METERS) - 200.0),
                        point: {
                            pixelSize: 8,
                            color: Cesium.Color.fromCssColorString('#10B981'),
                            outlineColor: Cesium.Color.BLACK,
                            outlineWidth: 0,
                            // ATTACHMENT FIX: Push path into background
                            // @ts-ignore
                            eyeOffset: new Cesium.Cartesian3(0, 0, 500.0),
                            heightReference: Cesium.HeightReference.NONE
                        }
                    });
                    waypointMarkerEntities.set(`waypoint-${index}`, entity);
                });
            }
        });

        signalRService.onReceiveFlightData((data: any) => {
            if (!currentViewer || !data) return;

            // Extract from DTO (Backend uses PascalCase for the object properties)
            const flightId = "UAV-100";
            const lat = data.lat || data.Lat;
            const lng = data.lng || data.Lng;
            const heading = data.heading || data.Heading || 0; // Default to 0 if missing
            const altitude = data.altitude || data.Altitude;
            const speed = data.speed || data.Speed;
            const targetLat = data.targetLat || data.TargetLat;
            const targetLng = data.targetLng || data.TargetLng;
            const payloadPitch = data.payloadPitch || data.PayloadPitch;
            const payloadYaw = data.payloadYaw || data.PayloadYaw;
            const flightMode = data.mode || data.Mode;

            const position = Cesium.Cartesian3.fromDegrees(lng, lat, altitude * FEET_TO_METERS);
            
            currentFlightData.value = { 
                flightId, lat, lng, altitude, speed, heading, 
                payloadPitch, payloadYaw, flightMode 
            };
            flightTargets.set(flightId, { lat: targetLat, lng: targetLng });

            // --- STATE-DRIVEN CLEANUP ---
            if (flightMode === 'Orbiting') {
                const distToTarget = Math.sqrt(Math.pow(lat - targetLat, 2) + Math.pow(lng - targetLng, 2));
                if (distToTarget < 0.032 && flightPathCache.has('active')) {
                    clearOptimalPath();
                }
            }

            // 1. Projected Path (Digital Pulse Beam)
            if (!projectedPathEntities.has(flightId)) {
                const entity = currentViewer.entities.add({
                    polyline: {
                        positions: new Cesium.CallbackProperty(() => {
                            const currentData = currentFlightData.value;
                            const target = flightTargets.get(flightId);
                            if (!currentData || !target || !viewer.value) return []; 
                            
                            // SYNC FIX: Get live interpolated position from the UAV entity
                            const uav = uavEntities.get(flightId);
                            const uavPos = uav?.position?.getValue(currentViewer.clock.currentTime);
                            if (!uavPos) return [];

                            // ATTACHMENT FIX: Offset beam start to the NOSE of the UAV
                            const carto = Cesium.Cartographic.fromCartesian(uavPos);
                            const headingRad = Cesium.Math.toRadians(currentData.heading);
                            const noseDist = 6.0; // 6 meters forward
                            const noseLat = carto.latitude + (noseDist / 6371000) * Math.cos(headingRad);
                            const noseLng = carto.longitude + (noseDist / (6371000 * Math.cos(carto.latitude))) * Math.sin(headingRad);
                            const uavNosePos = Cesium.Cartesian3.fromRadians(noseLng, noseLat, carto.height - 20.0);

                            let beamTargetLng = target.lng;
                            let beamTargetLat = target.lat;
                            let beamTargetAlt = 0;

                            const path = flightPathCache.get('active');
                            if (path && path.length > 1) {
                                // SEQUENTIAL FIX: Find the first point we haven't 'hit' yet in order
                                let nextIdx = 1;
                                for (let i = 1; i < path.length; i++) {
                                    const p = path[i];
                                    const dist = Math.sqrt(Math.pow(p.lat - currentData.lat, 2) + Math.pow(p.lng - currentData.lng, 2));
                                    if (dist > 0.0003) { // 33m threshold
                                        nextIdx = i;
                                        break;
                                    }
                                    nextIdx = i;
                                }

                                const nextWaypoint = path[nextIdx];
                                if (nextWaypoint && nextIdx < path.length - 1) {
                                    beamTargetLng = nextWaypoint.lng;
                                    beamTargetLat = nextWaypoint.lat;
                                    beamTargetAlt = ((nextWaypoint.altitudeFt || 0) * FEET_TO_METERS) - 200.0;
                                } else {
                                    beamTargetLng = target.lng;
                                    beamTargetLat = target.lat;
                                    beamTargetAlt = targetHeightCache.get('final') || 0;
                                }
                            } else {
                                beamTargetAlt = targetHeightCache.get(flightId) || 0;
                            }

                            return [
                                uavNosePos, 
                                Cesium.Cartesian3.fromDegrees(beamTargetLng, beamTargetLat, beamTargetAlt)
                            ];
                        }, false),
                        width: 8,
                        // @ts-ignore
                        material: new TacticalBeamMaterialProperty({
                            color: Cesium.Color.fromCssColorString('#00F2FF'),
                            speed: 2.0,
                            contextId: 'main'
                        }),
                        // Push beam into background relative to UAV
                        // @ts-ignore
                        eyeOffset: new Cesium.Cartesian3(0, 0, 50.0)
                    }
                });
                projectedPathEntities.set(flightId, entity);
            }

            // DEFINITIVE VISIBILITY: Only show beam if transiting AND no safe path is active
            const beamEntity = projectedPathEntities.get(flightId);
            if (beamEntity) {
                // Show cyan beam ONLY if:
                // 1. We are Transiting
                // 2. We are NOT currently following a calculated safe path (green line)
                beamEntity.show = (flightMode === 'Transiting' && !optimalPathEntity.value) as any;
            }

            // 2. UAV Marker (Procedural 3D Shape)
            const headingRadians = Cesium.Math.toRadians(heading + 90); 
            const pitchRadians = 0;
            const rollRadians = 0;
            const hpr = new Cesium.HeadingPitchRoll(headingRadians, pitchRadians, rollRadians);
            
            const orientation = Cesium.Transforms.headingPitchRollQuaternion(position, hpr);

            if (uavEntities.has(flightId)) {
                const entity = uavEntities.get(flightId);
                if (entity) {
                    const futureTime = Cesium.JulianDate.now();
                    entity.orientation = orientation as any;
                    
                    if (entity.position instanceof Cesium.SampledPositionProperty) {
                        entity.position.addSample(futureTime, position);
                    }
                }
            } else {
                console.log("Creating 3D UAV Model for", flightId);
                
                if (!currentViewer.clock.shouldAnimate) {
                    currentViewer.clock.shouldAnimate = true;
                }

                const sampledPosition = new Cesium.SampledPositionProperty();
                const now = Cesium.JulianDate.now();
                sampledPosition.addSample(now, position);
                sampledPosition.forwardExtrapolationType = Cesium.ExtrapolationType.HOLD;

                const entity = currentViewer.entities.add({
                    position: sampledPosition,
                    orientation: orientation,
                    // Dynamic Scaling: Small when close, Huge on globe
                    model: {
                        // Unique URL for Main Map context to avoid sharing conflicts
                        uri: '/ORBITER4.gltf?v=main&cb=' + Math.random().toString(36).substring(7),
                        minimumPixelSize: 48, // Reduced floor for globe visibility
                        maximumScale: 5000, // Reduced max scale
                        // Refined: 0.1 scale at 1km (visible), 10.0 scale at 500km (huge)
                        // @ts-ignore
                        scaleByDistance: new Cesium.NearFarScalar(1.0e3, 0.1, 5.0e5, 10.0),
                        color: Cesium.Color.WHITE.withAlpha(1.0),
                        colorBlendMode: Cesium.ColorBlendMode.HIGHLIGHT,
                        heightReference: Cesium.HeightReference.NONE
                    },
                    // ATTACHMENT FIX: Aggressively pull UAV into the foreground layer
                    // @ts-ignore
                    eyeOffset: new Cesium.Cartesian3(0, 0, -500.0),
                });
                uavEntities.set(flightId, entity);

                // --- SENSOR FOOTPRINT & FRUSTUM (Physics Vectors & Horizontal FOV) ---
                const calculateFootprintCorners = (data: any): Cesium.Cartesian3[] => {
                    const currentViewer = viewer.value;
                    if (!currentViewer) return [];

                    // 1. SYNC: Use exact telemetry position
                    const exactPos = Cesium.Cartesian3.fromDegrees(
                        data.lng, 
                        data.lat, 
                        data.altitude * FEET_TO_METERS
                    );

                    // 2. FOV: Use Dynamic Sensor FOV (Vertical)
                    // Aspect = 16:9
                    const aspect = 16.0 / 9.0;
                    const vFovHalf = Cesium.Math.toRadians(sensorFov.value / 2.0); 
                    // Calculate Horizontal FOV from Vertical
                    const hFovHalf = Math.atan(Math.tan(vFovHalf) * aspect);

                    const tanH = Math.tan(hFovHalf);
                    const tanV = Math.tan(vFovHalf);

                    // 3. Sensor Angles
                    const yawRad = Cesium.Math.toRadians(data.payloadYaw);
                    const pitchRad = Cesium.Math.toRadians(data.payloadPitch);

                    // 4. Construct Basis Vectors (ENU Frame)
                    // Yaw 0 = North (+Y). Pitch 0 = Horizon. Pitch -90 = Down (-Z).
                    
                    // Forward Vector (Look Dir)
                    // X = East = sin(Yaw) * cos(Pitch)
                    // Y = North = cos(Yaw) * cos(Pitch)
                    // Z = Up = sin(Pitch)
                    const forward = new Cesium.Cartesian3(
                        Math.sin(yawRad) * Math.cos(pitchRad),
                        Math.cos(yawRad) * Math.cos(pitchRad),
                        Math.sin(pitchRad)
                    );
                    
                    // Global Up (for cross product)
                    const globalUp = Cesium.Cartesian3.UNIT_Z;
                    
                    // Right Vector = Forward x GlobalUp
                    // (Unless looking straight up/down, handled by normalization safety usually)
                    const right = new Cesium.Cartesian3();
                    Cesium.Cartesian3.cross(forward, globalUp, right);
                    Cesium.Cartesian3.normalize(right, right);

                    // Camera Up Vector = Right x Forward
                    const up = new Cesium.Cartesian3();
                    Cesium.Cartesian3.cross(right, forward, up);
                    Cesium.Cartesian3.normalize(up, up);

                    // 5. Transform from ENU to Fixed Frame (ECEF)
                    const enuToFixed = Cesium.Transforms.eastNorthUpToFixedFrame(exactPos);
                    const rotMatrix = Cesium.Matrix4.getMatrix3(enuToFixed, new Cesium.Matrix3());

                    // 6. Calculate 4 Corner Rays
                    const corners: Cesium.Cartesian3[] = [];
                    const multipliers = [
                        { h: -1, v: 1 },  // Top-Left
                        { h: 1, v: 1 },   // Top-Right
                        { h: 1, v: -1 },  // Bottom-Right
                        { h: -1, v: -1 }  // Bottom-Left
                    ];

                    for (const m of multipliers) {
                        // Ray = Forward + (Right * tanH * m.h) + (Up * tanV * m.v)
                        const hVec = new Cesium.Cartesian3();
                        const vVec = new Cesium.Cartesian3();
                        const rayLocal = new Cesium.Cartesian3();

                        Cesium.Cartesian3.multiplyByScalar(right, tanH * m.h, hVec);
                        Cesium.Cartesian3.multiplyByScalar(up, tanV * m.v, vVec);
                        
                        Cesium.Cartesian3.add(forward, hVec, rayLocal);
                        Cesium.Cartesian3.add(rayLocal, vVec, rayLocal);
                        Cesium.Cartesian3.normalize(rayLocal, rayLocal);

                        // Rotate to Global Fixed Frame
                        const rayGlobal = new Cesium.Cartesian3();
                        Cesium.Matrix3.multiplyByVector(rotMatrix, rayLocal, rayGlobal);

                        // 4. Cast Ray
                        const ray = new Cesium.Ray(exactPos, rayGlobal);
                        let intersection = currentViewer.scene.globe.pick(ray, currentViewer.scene);
                        const MAX_SENSOR_RANGE = 20000.0; // 20km Limit

                        // 5. Fallback to Ellipsoid
                        if (!intersection) {
                            const interval = Cesium.IntersectionTests.rayEllipsoid(ray, Cesium.Ellipsoid.WGS84);
                            if (interval) {
                                intersection = Cesium.Ray.getPoint(ray, interval.start);
                            }
                        }

                        // 6. Range Clamping (Sync with Video Fog)
                        if (intersection) {
                            const dist = Cesium.Cartesian3.distance(exactPos, intersection);
                            if (dist > MAX_SENSOR_RANGE) {
                                // Clamp to Max Range
                                intersection = Cesium.Ray.getPoint(ray, MAX_SENSOR_RANGE);
                            }
                            corners.push(intersection);
                        }
                    }

                    return corners;
                };

                // 1. Ground Footprint (Polygon)
                currentViewer.entities.add({
                    polygon: {
                        hierarchy: new Cesium.CallbackProperty(() => {
                            const data = currentFlightData.value;
                            if (!data) return undefined;
                            return new Cesium.PolygonHierarchy(calculateFootprintCorners(data));
                        }, false) as any,
                        material: Cesium.Color.LIME.withAlpha(0.5), // Increased opacity
                        // Draping Fix: Disable perPositionHeight to clamp to ground/terrain automatically
                        perPositionHeight: false, 
                        outline: false,
                        // Ensure it sits on top of terrain
                        heightReference: Cesium.HeightReference.CLAMP_TO_GROUND
                    }
                });

                // 2. Frustum Volume (4 Side Faces)
                for (let i = 0; i < 4; i++) {
                    const nextIdx = (i + 1) % 4;

                    currentViewer.entities.add({
                        polygon: {
                            hierarchy: new Cesium.CallbackProperty(() => {
                                const data = currentFlightData.value;
                                const uav = uavEntities.get(flightId);
                                if (!data || !uav || !viewer.value) return undefined;
                                
                                const rawPos = uav.position?.getValue(currentViewer.clock.currentTime);
                                if (!rawPos) return undefined;
                                
                                const carto = Cesium.Cartographic.fromCartesian(rawPos);
                                // FIX: Offset start position down by 20 meters to match beam attachment level
                                const uavBottomPos = Cesium.Cartesian3.fromRadians(carto.longitude, carto.latitude, carto.height - 20.0);

                                const corners = calculateFootprintCorners(data);
                                const c1 = corners[i];
                                const c2 = corners[nextIdx];
                                if (c1 === undefined || c2 === undefined) return undefined;
                                return new Cesium.PolygonHierarchy([uavBottomPos, c1, c2]);
                            }, false) as any,
                            material: Cesium.Color.LIME.withAlpha(0.15), // Increased opacity
                            perPositionHeight: true,
                            outline: false
                        }
                    });
                }
            }

            // Register HTML Label for UAV
            // Dynamic Label Logic: Show ETA/Dist when Transiting
            let subLabel: string | undefined = undefined;

            if (flightMode === 'Transiting' && speed > 1) {
                let totalDistance = 0;
                const path = flightPathCache.get('active');
                
                // If we have an active multi-point path (Optimal Route), calculate along the path
                if (path && path.length > 1) {
                    // 1. Find the closest point index to know where we are
                    // We assume the UAV is between index i and i+1
                    // Simple heuristic: Find the closest waypoint to current position
                    let closestIdx = 0;
                    let minGap = Infinity;
                    
                    for (let i = 0; i < path.length; i++) {
                        const p = path[i];
                        const gap = Math.pow(p.lat - lat, 2) + Math.pow(p.lng - lng, 2);
                        if (gap < minGap) {
                            minGap = gap;
                            closestIdx = i;
                        }
                    }
                    
                    // We need to determine if we passed closestIdx or are approaching it.
                    // Simplified: We sum from closestIdx+1 to end, plus distance from UAV to closestIdx+1
                    // Better approach: Since we are flying TO path[i+1], let's assume we are targeting the next point.
                    
                    // Let's iterate forward and sum segments
                    
                    // Distance from UAV to the next target waypoint (simplified assumption: usually closest+1)
                    // However, robust logic is: distance(UAV, path[closestIdx]) is negligible if we are ON the point
                    // Let's just sum segments starting from the one AFTER the closest point
                    // And add distance from UAV to that next point.
                    
                    // Fallback to simple logic: If index is last, just distance to end.
                    
                    // Calculate distance from UAV to path[closestIdx] (or next one if we passed it?)
                    // This is tricky without knowing segment progress.
                    // Alternative: Sum ALL segments from closestIdx to End.
                    // Total = Distance(UAV, path[closestIdx+1]) + Sum(path[closestIdx+1] -> End)
                    
                    const nextIdx = closestIdx < path.length - 1 ? closestIdx + 1 : closestIdx;
                    
                    // 1. UAV to Next Waypoint (High Precision WGS84 Geodesic)
                    const uavCartographic = Cesium.Cartographic.fromDegrees(lng, lat);
                    const nextCartographic = Cesium.Cartographic.fromDegrees(path[nextIdx].lng, path[nextIdx].lat);
                    
                    const geodesic = new Cesium.EllipsoidGeodesic();
                    geodesic.setEndPoints(uavCartographic, nextCartographic);
                    totalDistance += geodesic.surfaceDistance;

                    // 2. Sum remaining segments
                    for (let i = nextIdx; i < path.length - 1; i++) {
                        const p1 = path[i];
                        const p2 = path[i+1];
                        
                        const c1 = Cesium.Cartographic.fromDegrees(p1.lng, p1.lat);
                        const c2 = Cesium.Cartographic.fromDegrees(p2.lng, p2.lat);
                        
                        geodesic.setEndPoints(c1, c2);
                        totalDistance += geodesic.surfaceDistance;
                    }
                } else {
                    // Direct line fallback (WGS84)
                    const uavCartographic = Cesium.Cartographic.fromDegrees(lng, lat);
                    const targetCartographic = Cesium.Cartographic.fromDegrees(targetLng, targetLat);
                    
                    const geodesic = new Cesium.EllipsoidGeodesic();
                    geodesic.setEndPoints(uavCartographic, targetCartographic);
                    totalDistance = geodesic.surfaceDistance;
                }

                // Adjust for 3km Orbit Radius
                totalDistance = Math.max(0, totalDistance - 3000);

                const distStr = totalDistance > 1000 ? `${(totalDistance/1000).toFixed(2)}km` : `${Math.round(totalDistance)}m`;
                
                // FIX: Speed is in Knots. Convert to m/s for correct time.
                // 1 Knot = 0.514444 m/s
                const speedMs = speed * 0.514444;
                const timeSeconds = speedMs > 0 ? totalDistance / speedMs : 0;
                
                // Format time: "2m 30s" or "45s"
                let timeStr = '';
                if (timeSeconds > 60) {
                    timeStr = `${Math.floor(timeSeconds / 60)}m ${Math.round(timeSeconds % 60)}s`;
                } else {
                    timeStr = `${Math.round(timeSeconds)}s`;
                }

                subLabel = `DTG: ${distStr}|ETA: ${timeStr}`;
            }

            registerLabel(flightId, 'UAV 100', 'uav', () => {
                const entity = uavEntities.get(flightId);
                const currentViewer = viewer.value;
                if (!entity || !currentViewer || currentViewer.isDestroyed()) return undefined;
                
                try {
                    return entity.position?.getValue(currentViewer.clock.currentTime);
                } catch (e) {
                    return undefined;
                }
            }, { yOffset: 35, subLabel }); // Pass subLabel here
        });
    };

    const stopFlightListeners = async () => {
        await signalRService.stopConnection();
    };

    return {
        currentFlightData,
        sensorFov,
        initializeFlightListeners,
        stopFlightListeners
    };
}