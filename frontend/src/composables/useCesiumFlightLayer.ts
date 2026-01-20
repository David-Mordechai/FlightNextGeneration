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
    
    const currentFlightData = ref<{
        flightId: string;
        lat: number;
        lng: number;
        altitude: number;
        speed: number;
        heading: number;
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
                const lastPoint = pathData[pathData.length - 1];
                finalDestination.value = { lat: lastPoint.lat, lng: lastPoint.lng };

                // Cache height for the final destination to keep the beam stable
                const carto = Cesium.Cartographic.fromDegrees(lastPoint.lng, lastPoint.lat);
                Cesium.sampleTerrainMostDetailed(currentViewer.terrainProvider, [carto]).then(samples => {
                    if (samples && samples[0] && samples[0].height !== undefined) {
                        targetHeightCache.set('final', samples[0].height);
                    }
                });

                // Convert points to Cartesian3
                const positions = pathData.map(p => Cesium.Cartesian3.fromDegrees(p.lng, p.lat, 0)); 

                optimalPathEntity.value = currentViewer.entities.add({
                    polyline: {
                        positions: positions,
                        width: 5,
                        material: new Cesium.PolylineGlowMaterialProperty({
                            color: Cesium.Color.fromCssColorString('#10B981'),
                            glowPower: 0.2
                        }),
                        clampToGround: true
                    }
                });

                // Add waypoint markers
                pathData.forEach((p, index) => {
                    const entity = currentViewer.entities.add({
                        position: Cesium.Cartesian3.fromDegrees(p.lng, p.lat, 0),
                        point: {
                            pixelSize: 8,
                            color: Cesium.Color.fromCssColorString('#10B981'),
                            outlineColor: Cesium.Color.BLACK,
                            outlineWidth: 2,
                            heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
                            disableDepthTestDistance: Number.POSITIVE_INFINITY 
                        }
                    });
                    waypointMarkerEntities.set(`waypoint-${index}`, entity);
                });
            }
        });

        signalRService.onReceiveFlightData((flightId: string, lat: number, lng: number, heading: number, altitude: number, speed: number, targetLat: number, targetLng: number) => {
            if (!currentViewer) return;

            const position = Cesium.Cartesian3.fromDegrees(lng, lat, altitude * FEET_TO_METERS);
            
            currentFlightData.value = { flightId, lat, lng, altitude, speed, heading };
            flightTargets.set(flightId, { lat: targetLat, lng: targetLng });

            // Ensure we have a cached height for this specific flight's target
            if (!targetHeightCache.has(flightId)) {
                targetHeightCache.set(flightId, 0); // Default
                const carto = Cesium.Cartographic.fromDegrees(targetLng, targetLat);
                Cesium.sampleTerrainMostDetailed(currentViewer.terrainProvider, [carto]).then(samples => {
                    if (samples && samples[0] && samples[0].height !== undefined) {
                        targetHeightCache.set(flightId, samples[0].height);
                    }
                });
            }

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
                            if (!currentData || !target) return []; 
                            
                            const terrainHeight = targetHeightCache.get(flightId) || 0;

                            return [
                                Cesium.Cartesian3.fromDegrees(currentData.lng, currentData.lat, currentData.altitude * FEET_TO_METERS),
                                Cesium.Cartesian3.fromDegrees(target.lng, target.lat, terrainHeight)
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
            const headingRadians = Cesium.Math.toRadians(heading - 90); 
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
                    } else {
                        const sampled = new Cesium.SampledPositionProperty();
                        sampled.addSample(futureTime, position);
                        entity.position = sampled;
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
                        uri: 'https://raw.githubusercontent.com/CesiumGS/cesium/main/Apps/SampleData/models/CesiumAir/Cesium_Air.glb',
                        minimumPixelSize: 128, 
                        maximumScale: 10000,
                        scale: 15.0, 
                        color: Cesium.Color.WHITE.withAlpha(1.0),
                        colorBlendMode: Cesium.ColorBlendMode.HIGHLIGHT,
                        heightReference: Cesium.HeightReference.NONE
                    },
                    // Add a trail
                    path: {
                        resolution: 1,
                        material: new Cesium.PolylineGlowMaterialProperty({
                            glowPower: 0.1,
                            color: Cesium.Color.CYAN
                        }),
                        width: 5,
                        leadTime: 0,
                        trailTime: 5 // 5 Seconds trail
                    },
                    // NO CESIUM LABEL
                });
                uavEntities.set(flightId, entity);
            }

            // Register HTML Label for UAV
            registerLabel(flightId, `UAV-${flightId}`, 'uav', () => {
                const entity = uavEntities.get(flightId);
                const currentViewer = viewer.value;
                if (!entity || !currentViewer || currentViewer.isDestroyed()) return undefined;
                
                try {
                    // Use currentTime for perfectly synced interpolation
                    return entity.position?.getValue(currentViewer.clock.currentTime);
                } catch (e) {
                    return undefined;
                }
            }, { yOffset: 60 });
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