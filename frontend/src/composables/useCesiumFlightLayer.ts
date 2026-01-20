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
            const heading = data.heading || data.Heading;
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
                            speed: 2.0
                        }),
                        // Push beam into background relative to UAV
                        // @ts-ignore
                        eyeOffset: new Cesium.Cartesian3(0, 0, 50.0),
                        depthFailMaterial: new Cesium.PolylineGlowMaterialProperty({
                            glowPower: 0.1,
                            color: Cesium.Color.fromCssColorString('#00F2FF').withAlpha(0.2)
                        })
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
                        uri: '/ORBITER4.gltf',
                        minimumPixelSize: 128, // Floor for globe visibility
                        maximumScale: 10000,
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

                // --- SENSOR FOOTPRINT & FRUSTUM (Aspect Ratio Fix) ---
                const calculateFootprintCorners = (data: any): Cesium.Cartesian3[] => {
                    const uav = uavEntities.get(flightId);
                    const uavPos = uav?.position?.getValue(currentViewer.clock.currentTime);
                    if (!uavPos) return [];

                    const altMeters = data.altitude * FEET_TO_METERS;
                    const yawRad = Cesium.Math.toRadians(data.payloadYaw);
                    const pitchRad = Cesium.Math.toRadians(data.payloadPitch);
                    
                    // Match 16:9 Aspect Ratio
                    const halfFovY = Cesium.Math.toRadians(7.5); // 15 deg total height
                    const halfFovX = Cesium.Math.toRadians(12.5); // 25 deg total width

                    const corners: Cesium.Cartesian3[] = [];
                    // Calculate the 4 corners of the 16:9 camera frustum intersection
                    const offsets = [
                        { p: -halfFovY, y: -halfFovX },
                        { p: -halfFovY, y: halfFovX },
                        { p: halfFovY, y: halfFovX },
                        { p: halfFovY, y: -halfFovX }
                    ];

                    for (const offset of offsets) {
                        const effectivePitch = pitchRad + offset.p;
                        const effectiveYaw = yawRad + offset.y;
                        
                        const groundDist = altMeters / Math.tan(Math.abs(effectivePitch));
                        const latOffset = (groundDist / 111320) * Math.cos(effectiveYaw);
                        const lngOffset = (groundDist / (111320 * Math.cos(Cesium.Math.toRadians(data.lat)))) * Math.sin(effectiveYaw);
                        
                        corners.push(Cesium.Cartesian3.fromDegrees(data.lng + lngOffset, data.lat + latOffset, 0.2));
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
                        perPositionHeight: true, 
                        outline: false
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
            registerLabel(flightId, 'UAV 100', 'uav', () => {
                const entity = uavEntities.get(flightId);
                const currentViewer = viewer.value;
                if (!entity || !currentViewer || currentViewer.isDestroyed()) return undefined;
                
                try {
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