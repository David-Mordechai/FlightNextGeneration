import { ref, markRaw } from 'vue';
import L from 'leaflet';
import { signalRService } from '../services/SignalRService';
import { createUavIcon } from '../utils/leafletSetup';

export function useFlightVisualization(map: any) {
    const markers = ref<Map<string, L.Marker>>(new Map());
    const paths = ref<Map<string, L.Polyline>>(new Map());
    const projectedPaths = ref<Map<string, L.Polyline>>(new Map());
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

            // 1. Historical Path
            if (!paths.value.has(flightId)) {
                const polyline = L.polyline([], {
                    color: '#3B82F6', 
                    weight: 4,
                    opacity: 0.8,
                    smoothFactor: 0.5
                }).addTo(currentMap);
                paths.value.set(flightId, markRaw(polyline));
            }
            
            const path = paths.value.get(flightId);
            if (path) {
                path.addLatLng([lat, lng]);
                const latLngs = path.getLatLngs() as L.LatLng[];
                if (latLngs.length > 2000) {
                    path.setLatLngs(latLngs.slice(latLngs.length - 2000));
                }
            }

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
