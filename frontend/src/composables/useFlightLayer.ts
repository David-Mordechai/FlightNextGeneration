import { ref, markRaw } from 'vue';
import L from 'leaflet';
import { signalRService } from '../services/SignalRService';
import { createUavIcon } from '../utils/leafletSetup';

export function useFlightVisualization(map: any) {
    const markers = ref<Map<string, L.Marker>>(new Map());
    const projectedPaths = ref<Map<string, L.Polyline>>(new Map());
    const optimalPathLayer = ref<L.Polyline | null>(null);
    const pathMarkersLayer = ref<L.LayerGroup | null>(null); // Layer for turning point dots
    const hasCentered = ref(false);
    const isZooming = ref(false);
    const finalDestination = ref<{lat: number, lng: number} | null>(null);
    
    const currentFlightData = ref<{
        flightId: string;
        lat: number;
        lng: number;
        altitude: number;
        speed: number;
        heading: number;
    } | null>(null);

    const clearOptimalPath = (currentMap: L.Map) => {
        if (optimalPathLayer.value) {
            currentMap.removeLayer(optimalPathLayer.value as unknown as L.Layer);
            optimalPathLayer.value = null;
        }
        if (pathMarkersLayer.value) {
            currentMap.removeLayer(pathMarkersLayer.value as unknown as L.Layer);
            pathMarkersLayer.value = null;
        }
        finalDestination.value = null;
    };

    const initializeFlightListeners = () => {
        const currentMap = map.value;
        if (!currentMap) return;

        // Zoom Listeners to prevent update artifacts
        currentMap.on('zoomstart', () => { isZooming.value = true; });
        currentMap.on('zoomend', () => { 
            isZooming.value = false; 
        });

        // Listen for Optimal Route
        signalRService.on('RouteCalculated', (pathData: any[]) => {
            if (!currentMap) return;

            // Clear previous
            clearOptimalPath(currentMap);
            
            // Store final destination for arrival check
            if (pathData && pathData.length > 0) {
                const lastPoint = pathData[pathData.length - 1];
                finalDestination.value = { lat: lastPoint.lat, lng: lastPoint.lng };
            }

            pathMarkersLayer.value = L.layerGroup().addTo(currentMap);

            // Convert [{lat, lng}, ...] to [[lat, lng], ...]
            const latLngs = pathData.map(p => [p.lat, p.lng] as [number, number]);

            // Draw thinner line (Weight 2)
            optimalPathLayer.value = L.polyline(latLngs, {
                color: '#10B981', // Green-500
                weight: 2,
                opacity: 0.8,
                className: 'flight-path-optimal',
                pane: 'markerPane' // Ensure line is above labels (shadowPane)
            }).addTo(currentMap);

            // Add circle markers at turning points
            latLngs.forEach((point) => {
                if (!pathMarkersLayer.value) return;
                
                L.circleMarker(point, {
                    radius: 3,
                    color: '#10B981',      
                    fillColor: '#10B981',  
                    fillOpacity: 0.6,
                    weight: 1,
                    className: 'waypoint-dot', 
                    pane: 'markerPane' // Ensure dots are above labels
                }).addTo(pathMarkersLayer.value as unknown as L.LayerGroup);
            });
        });

        signalRService.onReceiveFlightData((flightId: string, lat: number, lng: number, heading: number, altitude: number, speed: number, targetLat: number, targetLng: number) => {
            if (!currentMap) return;

            // One-time centering
            if (!hasCentered.value) {
                currentMap.setView([lat, lng], 13);
                hasCentered.value = true;
            }

            currentFlightData.value = { flightId, lat, lng, altitude, speed, heading };
            const scale = Math.max(0.3, Math.min(1.8, 3000 / altitude));

            // --- Arrival Detection ---
            // Only clear if we have a valid final destination and we are close to IT (not just the next waypoint)
            if (finalDestination.value) {
                const distToFinal = Math.sqrt(Math.pow(lat - finalDestination.value.lat, 2) + Math.pow(lng - finalDestination.value.lng, 2));
                // Increased threshold to 0.012 (~1.3km) because UAV orbits at 0.01 (1km) radius.
                // It never reaches the exact center (0.0000), so we must detect arrival at the PERIMETER.
                if (distToFinal < 0.012) { 
                    clearOptimalPath(currentMap);
                }
            }

            // 1. Projected Path (Blue Laser)
            if (!isZooming.value) {
                if (!projectedPaths.value.has(flightId)) {
                    const projPolyline = L.polyline([], {
                        color: '#60A5FA',
                        weight: 2,
                        opacity: 0.9,
                        className: 'flight-path-projected',
                        pane: 'markerPane' // Ensure line is above labels
                    }).addTo(currentMap);
                    projectedPaths.value.set(flightId, markRaw(projPolyline));
                }

                const projPath = projectedPaths.value.get(flightId);
                if (projPath) {
                    projPath.setLatLngs([[lat, lng], [targetLat, targetLng]]);
                }
            }

            // 2. Marker
            if (markers.value.has(flightId)) {
                const marker = markers.value.get(flightId);
                if (marker) {
                    marker.setLatLng([lat, lng]);
                    const iconImg = marker.getElement()?.querySelector('.uav-icon') as HTMLElement;
                    if (iconImg) {
                        iconImg.style.transformOrigin = 'center';
                        iconImg.style.transform = `rotate(${heading}deg) scale(${scale})`;
                    }
                }
            } else {
                const newMarker = L.marker([lat, lng], { 
                    icon: createUavIcon(heading, altitude),
                    zIndexOffset: 2000 
                }).addTo(currentMap);
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
