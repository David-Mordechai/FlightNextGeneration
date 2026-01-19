import { ref, type ShallowRef } from 'vue';
import * as Cesium from 'cesium';
import { signalRService } from '../services/SignalRService';

export function useCesiumFlightVisualization(viewer: ShallowRef<Cesium.Viewer | null>) {
    const uavEntities = new Map<string, Cesium.Entity>();
    const projectedPathEntities = new Map<string, Cesium.Entity>();
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

    const initializeFlightListeners = () => {
        const currentViewer = viewer.value;
        if (!currentViewer) return;

        // Listen for Optimal Route
        signalRService.on('RouteCalculated', (pathData: any[]) => {
            if (!currentViewer) return;

            clearOptimalPath();
            
            if (pathData && pathData.length > 0) {
                const lastPoint = pathData[pathData.length - 1];
                finalDestination.value = { lat: lastPoint.lat, lng: lastPoint.lng };

                // Convert points to Cartesian3
                const positions = pathData.map(p => Cesium.Cartesian3.fromDegrees(p.lng, p.lat, 100 * FEET_TO_METERS)); // Default altitude for path

                optimalPathEntity.value = currentViewer.entities.add({
                    polyline: {
                        positions: positions,
                        width: 3,
                        material: new Cesium.PolylineGlowMaterialProperty({
                            glowPower: 0.2,
                            color: Cesium.Color.fromCssColorString('#10B981')
                        }),
                        clampToGround: false
                    }
                });

                // Add waypoint markers
                pathData.forEach((p, index) => {
                    const entity = currentViewer.entities.add({
                        position: Cesium.Cartesian3.fromDegrees(p.lng, p.lat, 100 * FEET_TO_METERS),
                        point: {
                            pixelSize: 8,
                            color: Cesium.Color.fromCssColorString('#10B981'),
                            outlineColor: Cesium.Color.BLACK,
                            outlineWidth: 2,
                            disableDepthTestDistance: Number.POSITIVE_INFINITY // Always visible
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

            // One-time centering
            if (!hasCentered.value) {
                currentViewer.camera.flyTo({
                    destination: Cesium.Cartesian3.fromDegrees(lng, lat, (altitude * FEET_TO_METERS) + 2000),
                    orientation: {
                        heading: Cesium.Math.toRadians(0),
                        pitch: Cesium.Math.toRadians(-45),
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

            // 1. Projected Path (Blue Laser)
            if (!projectedPathEntities.has(flightId)) {
                const entity = currentViewer.entities.add({
                    polyline: {
                        positions: new Cesium.CallbackProperty(() => {
                            const currentData = currentFlightData.value;
                            if (!currentData) return [];
                            return [
                                Cesium.Cartesian3.fromDegrees(currentData.lng, currentData.lat, currentData.altitude * FEET_TO_METERS),
                                Cesium.Cartesian3.fromDegrees(targetLng, targetLat, currentData.altitude * FEET_TO_METERS)
                            ];
                        }, false),
                        width: 6, 
                        material: new Cesium.PolylineGlowMaterialProperty({
                            glowPower: 0.5,
                            color: Cesium.Color.fromCssColorString('#60A5FA')
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
            
            // For SampledPosition, we should use a Callback for orientation too if we want it to stay perfectly synced
            // but setting it once per SignalR update is usually enough.
            const orientation = Cesium.Transforms.headingPitchRollQuaternion(position, hpr);

            if (uavEntities.has(flightId)) {
                const entity = uavEntities.get(flightId);
                if (entity) {
                    // Add sample 1 second into the future to allow Cesium to interpolate smoothly
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
                        minimumPixelSize: 64, // Standard tactical size
                        maximumScale: 10000,
                        scale: 5.0, // Reasonable physical scale
                        silhouetteColor: Cesium.Color.AQUA,
                        silhouetteSize: 2.0,
                        color: Cesium.Color.WHITE.withAlpha(1.0),
                        colorBlendMode: Cesium.ColorBlendMode.HIGHLIGHT,
                        heightReference: Cesium.HeightReference.NONE
                    },
                    label: {
                        text: `UAV-${flightId}`,
                        font: 'bold 18px monospace',
                        fillColor: Cesium.Color.AQUA,
                        outlineColor: Cesium.Color.BLACK,
                        outlineWidth: 3,
                        style: Cesium.LabelStyle.FILL_AND_OUTLINE,
                        verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
                        pixelOffset: new Cesium.Cartesian2(0, -50),
                        disableDepthTestDistance: Number.POSITIVE_INFINITY
                    }
                });
                uavEntities.set(flightId, entity);
            }
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
