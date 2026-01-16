import { ref, markRaw } from 'vue';
import L from 'leaflet';
import { signalRService } from '../services/SignalRService';
import { createUavIcon } from '../utils/leafletSetup';

export function useFlightVisualization(map: any) {
    const markers = ref<Map<string, L.Marker>>(new Map());
    const projectedPaths = ref<Map<string, L.Polyline>>(new Map());
    const optimalPathLayer = ref<L.Polyline | null>(null);
    const hasCentered = ref(false);
    
    const currentFlightData = ref<{
        flightId: string;
        lat: number;
        lng: number;
        altitude: number;
        speed: number;
        heading: number;
    } | null>(null);

    const initializeFlightListeners = () => {
        // Listen for Optimal Route
        signalRService.on('RouteCalculated', (pathData: any[]) => {
            const currentMap = map.value;
            if (!currentMap) return;

            if (optimalPathLayer.value) {
                currentMap.removeLayer(optimalPathLayer.value);
            }

            // Convert [{lat, lng}, ...] to [[lat, lng], ...]
            const latLngs = pathData.map(p => [p.lat, p.lng] as [number, number]);

            optimalPathLayer.value = L.polyline(latLngs, {
                color: '#10B981', // Green-500
                weight: 4,
                dashArray: '10, 10',
                opacity: 0.9
            }).addTo(currentMap);
        });

        signalRService.onReceiveFlightData((flightId: string, lat: number, lng: number, heading: number, altitude: number, speed: number, targetLat: number, targetLng: number) => {
            const currentMap = map.value;
            if (!currentMap) return;

            // One-time centering
            if (!hasCentered.value) {
                currentMap.setView([lat, lng], 13);
                hasCentered.value = true;
            }

            currentFlightData.value = { flightId, lat, lng, altitude, speed, heading };
            const scale = Math.max(0.3, Math.min(1.8, 3000 / altitude));

            // 1. Historical Path - REMOVED

            // 2. Projected Path
            if (!projectedPaths.value.has(flightId)) {
                const projPolyline = L.polyline([], {
                    color: '#60A5FA',
                    weight: 3,
                    dashArray: '10, 15', 
                    opacity: 0.9
                }).addTo(currentMap);
                projectedPaths.value.set(flightId, markRaw(projPolyline));
            }

            const projPath = projectedPaths.value.get(flightId);
            if (projPath) {
                projPath.setLatLngs([[lat, lng], [targetLat, targetLng]]);
            }

            // 3. Marker
            if (markers.value.has(flightId)) {
                const marker = markers.value.get(flightId);
                if (marker) {
                    marker.setLatLng([lat, lng]);
                    const iconImg = marker.getElement()?.querySelector('.uav-icon') as HTMLElement;
                    if (iconImg) {
                        iconImg.style.transform = `rotate(${heading}deg) scale(${scale})`;
                    }
                }
            } else {
                const newMarker = L.marker([lat, lng], { icon: createUavIcon(heading, altitude) })
                    .addTo(currentMap as any);
                markers.value.set(flightId, newMarker);
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
