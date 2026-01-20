import { ref, type ShallowRef } from 'vue';
import * as Cesium from 'cesium';
import { signalRService } from '../services/SignalRService';
import { TacticalBeamMaterialProperty, registerCustomMaterials } from '../utils/CesiumAdvancedMaterials';
import { useScreenLabels } from './useScreenLabels';

export function useCesiumFlightVisualization(viewer: ShallowRef<Cesium.Viewer | null>) {
    // Ensure custom materials are registered
    registerCustomMaterials();
    
    // Use Shared Label System
    const { registerLabel } = useScreenLabels();

    const uavEntities = new Map<string, Cesium.Entity>();
    const projectedPathEntities = new Map<string, Cesium.Entity>();
    const flightTargets = new Map<string, { lat: number, lng: number }>(); 
    const optimalPathEntity = ref<Cesium.Entity | null>(null);
    const waypointMarkerEntities = new Map<string, Cesium.Entity>();
    
    const hasCentered = ref(false);
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
    } | null>(null);

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

        // Listen for Optimal Route
        signalRService.on('RouteCalculated', (pathData: any[]) => {
            if (!currentViewer) return;

            clearOptimalPath();
            
            // CRITICAL: Polylines need at least 2 points to render without crashing
            if (pathData && pathData.length >= 2) {
                // Cache the path for the beam logic
                // Using a generic key or specific flightId if available. 
                // Since SignalR RouteCalculated might not have flightId yet, we'll store it as 'global' or use first data received.
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
                const positions = pathData.map(p => Cesium.Cartesian3.fromDegrees(p.lng, p.lat, (p.altitudeFt || 0) * FEET_TO_METERS)); 

                optimalPathEntity.value = currentViewer.entities.add({
                    polyline: {
                        positions: positions,
                        width: 5,
                        material: new Cesium.PolylineGlowMaterialProperty({
                            color: Cesium.Color.fromCssColorString('#10B981'),
                            glowPower: 0.2
                        }),
                        clampToGround: false // Show actual altitude
                    }
                });

                // Add waypoint markers
                pathData.forEach((p, index) => {
                    const entity = currentViewer.entities.add({
                        position: Cesium.Cartesian3.fromDegrees(p.lng, p.lat, (p.altitudeFt || 0) * FEET_TO_METERS),
                        point: {
                            pixelSize: 8,
                            color: Cesium.Color.fromCssColorString('#10B981'),
                            outlineColor: Cesium.Color.BLACK,
                            outlineWidth: 2,
                            heightReference: Cesium.HeightReference.NONE, // Show actual altitude
                            disableDepthTestDistance: Number.POSITIVE_INFINITY 
                        }
                    });
                    waypointMarkerEntities.set(`waypoint-${index}`, entity);
                });
            }
        });

        signalRService.onReceiveFlightData((flightId, lat, lng, heading, altitude, speed, targetLat, targetLng, payloadPitch, payloadYaw) => {
            if (!currentViewer) return;

            const position = Cesium.Cartesian3.fromDegrees(lng, lat, altitude * FEET_TO_METERS);
            
            currentFlightData.value = { flightId, lat, lng, altitude, speed, heading, payloadPitch, payloadYaw };
            flightTargets.set(flightId, { lat: targetLat, lng: targetLng });

            // One-time centering (Top-Down)
            if (!hasCentered.value) {
                currentViewer.camera.flyTo({
                    destination: Cesium.Cartesian3.fromDegrees(lng, lat, (altitude * FEET_TO_METERS) + 3000),
                    orientation: {
                        heading: Cesium.Math.toRadians(0),
                        pitch: Cesium.Math.toRadians(-90),
                        roll: 0.0
                    }
                });
                hasCentered.value = true;
            }

            // --- Arrival Detection ---
            if (finalDestination.value) {
                const distToFinal = Math.sqrt(Math.pow(lat - finalDestination.value.lat, 2) + Math.pow(lng - finalDestination.value.lng, 2));
                if (distToFinal < 0.012) { 
                    clearOptimalPath();
                }
            }

            // 1. Projected Path (Digital Pulse Beam)
            if (!projectedPathEntities.has(flightId)) {
                const entity = currentViewer.entities.add({
                    show: new Cesium.CallbackProperty(() => {
                        return !!(currentFlightData.value && flightTargets.get(flightId));
                    }, false) as any,
                    polyline: {
                        positions: new Cesium.CallbackProperty(() => {
                            const currentData = currentFlightData.value;
                            const target = flightTargets.get(flightId);
                            if (!currentData || !target || !viewer.value) return []; 
                            
                            // SYNC FIX: Get live interpolated position from the UAV entity
                            const uav = uavEntities.get(flightId);
                            const uavPos = uav?.position?.getValue(viewer.value.clock.currentTime);
                            if (!uavPos) return [];

                            // Beam Logic: Point to NEXT waypoint in optimal path, OR to the final target
                            let beamTargetLng = target.lng;
                            let beamTargetLat = target.lat;
                            let beamTargetAlt = 0;

                            const path = flightPathCache.get('active');
                            if (path && path.length > 0) {
                                // Find the first waypoint in the path that we haven't reached yet
                                const nextWaypoint = path.find(p => {
                                    const dist = Math.sqrt(Math.pow(p.lat - currentData.lat, 2) + Math.pow(p.lng - currentData.lng, 2));
                                    return dist > 0.00015; 
                                });

                                if (nextWaypoint) {
                                    beamTargetLng = nextWaypoint.lng;
                                    beamTargetLat = nextWaypoint.lat;
                                    beamTargetAlt = (nextWaypoint.altitudeFt || 0) * FEET_TO_METERS;
                                } else {
                                    beamTargetAlt = targetHeightCache.get('final') || 0;
                                }
                            }
                            else {
                                beamTargetAlt = targetHeightCache.get(flightId) || 0;
                            }

                            return [
                                uavPos, // Start exactly at the moving 3D model
                                Cesium.Cartesian3.fromDegrees(beamTargetLng, beamTargetLat, beamTargetAlt)
                            ];
                        }, false),
                        width: 8,
                        // @ts-ignore
                        material: new TacticalBeamMaterialProperty({
                            color: Cesium.Color.fromCssColorString('#00F2FF'),
                            speed: 2.0
                        }),
                        depthFailMaterial: new Cesium.PolylineGlowMaterialProperty({
                            glowPower: 0.1,
                            color: Cesium.Color.fromCssColorString('#00F2FF').withAlpha(0.2)
                        })
                    }
                });
                projectedPathEntities.set(flightId, entity);
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
                    // Using a high-quality public 3D aircraft model
                    model: {
                        uri: '/ORBITER4.gltf',
                        minimumPixelSize: 120, // Large minimum size to stay visible on the globe
                        maximumScale: 10000,
                        scale: 0.5, // Reasonable base scale for close-up
                        color: Cesium.Color.WHITE.withAlpha(1.0),
                        colorBlendMode: Cesium.ColorBlendMode.HIGHLIGHT,
                        heightReference: Cesium.HeightReference.NONE
                    },
                });
                uavEntities.set(flightId, entity);

                // --- SENSOR FOOTPRINT & FRUSTUM (Distance-Aware Fix) ---
                const calculateFootprintCorners = (data: any): Cesium.Cartesian3[] => {
                    const uav = uavEntities.get(flightId);
                    const uavPos = uav?.position?.getValue(currentViewer.clock.currentTime);
                    if (!uavPos) return [];

                    const altMeters = data.altitude * FEET_TO_METERS;
                    const yawRad = Cesium.Math.toRadians(data.payloadYaw);
                    const pitchRad = Cesium.Math.toRadians(data.payloadPitch);
                    
                    // 1. Calculate the ground intersection point
                    // Slant range distance on ground
                    const groundDist = altMeters / Math.tan(Math.abs(pitchRad));
                    
                    // Degrees offset (approximate with earth curvature correction)
                    const latOffset = (groundDist / 111320) * Math.cos(yawRad);
                    const lngOffset = (groundDist / (111320 * Math.cos(Cesium.Math.toRadians(data.lat)))) * Math.sin(yawRad);
                    
                    const centerLat = data.lat + latOffset;
                    const centerLng = data.lng + lngOffset;

                    // 2. Calculate corners around the intercept point
                    // Focused Tactical FOV (approx 10 deg total)
                    const slantRange = altMeters / Math.sin(Math.abs(pitchRad));
                    const widthMeters = slantRange * Math.tan(Cesium.Math.toRadians(5)); // 5 deg half-angle
                    const wDeg = widthMeters / 111320;

                    const corners: Cesium.Cartesian3[] = [
                        Cesium.Cartesian3.fromDegrees(centerLng - wDeg, centerLat - wDeg, 0),
                        Cesium.Cartesian3.fromDegrees(centerLng + wDeg, centerLat - wDeg, 0),
                        Cesium.Cartesian3.fromDegrees(centerLng + wDeg, centerLat + wDeg, 0),
                        Cesium.Cartesian3.fromDegrees(centerLng - wDeg, centerLat + wDeg, 0)
                    ];

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
                        material: Cesium.Color.LIME.withAlpha(0.2),
                        heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
                        outline: false
                    }
                });

                // 2. Frustum Volume (4 Side Faces)
                for (let i = 0; i < 4; i++) {
                    const nextIdx = (i + 1) % 4;

                    // Side Face (Volume)
                    currentViewer.entities.add({
                        polygon: {
                            hierarchy: new Cesium.CallbackProperty(() => {
                                const data = currentFlightData.value;
                                const uav = uavEntities.get(flightId);
                                if (!data || !uav || !viewer.value) return undefined;
                                const uavPos = uav.position?.getValue(currentViewer.clock.currentTime);
                                if (!uavPos) return undefined;
                                const corners = calculateFootprintCorners(data);
                                const c1 = corners[i];
                                const c2 = corners[nextIdx];
                                if (c1 === undefined || c2 === undefined) return undefined;
                                return new Cesium.PolygonHierarchy([uavPos, c1, c2]);
                            }, false) as any,
                            material: Cesium.Color.LIME.withAlpha(0.08),
                            perPositionHeight: true,
                            outline: false
                        }
                    });
                }
            }

            // Register HTML Label for UAV
            registerLabel(flightId, 'UAV 100', 'uav', () => {
                const entity = uavEntities.get(flightId);
                const currentViewer = viewer.value;
                if (!entity || !currentViewer || currentViewer.isDestroyed()) return undefined;
                
                try {
                    // Use currentTime for perfectly synced interpolation
                    return entity.position?.getValue(currentViewer.clock.currentTime);
                } catch (e) {
                    return undefined;
                }
            }, { yOffset: 35 });
        });
    };

    const stopFlightListeners = async () => {
        await signalRService.stopConnection();
    };

    return {
        currentFlightData,
        initializeFlightListeners,
        stopFlightListeners
    };
}