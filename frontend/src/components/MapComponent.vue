<script setup lang="ts">
import { onMounted, onUnmounted, ref, markRaw } from 'vue';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { signalRService } from '../services/SignalRService';
import FlightDataOverlay from './FlightDataOverlay.vue';
import MissionChat from './MissionChat.vue';

const mapContainer = ref<HTMLElement | null>(null);
const map = ref<L.Map | null>(null);
const markers = ref<Map<string, L.Marker>>(new Map());
const paths = ref<Map<string, L.Polyline>>(new Map());
const projectedPaths = ref<Map<string, L.Polyline>>(new Map());

// Reactive state for flight data
const currentFlightData = ref<{
  flightId: string;
  lat: number;
  lng: number;
  altitude: number;
  speed: number;
  heading: number;
} | null>(null);

onMounted(async () => {
  if (mapContainer.value) {
    // Set default view to Israel (approx center)
    const leafletMap = L.map(mapContainer.value, {
      zoomControl: false // Move zoom control or hide it to avoid conflict
    }).setView([31.0461, 34.8516], 8);
    
    // Move zoom control to bottom right
    L.control.zoom({ position: 'bottomright' }).addTo(leafletMap);

    map.value = leafletMap;

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    }).addTo(leafletMap);
    
    // Fix for default icon issues in some build environments
    delete (L.Icon.Default.prototype as any)._getIconUrl;
    L.Icon.Default.mergeOptions({
      iconRetinaUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-icon-2x.png',
      iconUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-icon.png',
      shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-shadow.png',
    });

    // Custom UAV Icon (DivIcon for rotation support)
    const createUavIcon = (heading: number, altitude: number) => {
      const scale = Math.max(0.3, Math.min(1.8, 3000 / altitude));
      return L.divIcon({
        className: 'uav-marker-container',
        html: `<img src="/uav.svg?v=4" class="uav-icon" style="transform: rotate(${heading}deg) scale(${scale}); width: 64px; height: 64px; transition: transform 0.05s linear;" />`,
        iconSize: [64, 64],
        iconAnchor: [32, 32],
        popupAnchor: [0, -32]
      });
    };

    // Listen for Flight Data
    signalRService.onReceiveFlightData((flightId: string, lat: number, lng: number, heading: number, altitude: number, speed: number, targetLat: number, targetLng: number) => {
      const currentMap = map.value;
      if (!currentMap) return;

      // Update reactive state for overlay
      currentFlightData.value = { flightId, lat, lng, altitude, speed, heading };

      const scale = Math.max(0.3, Math.min(1.8, 3000 / altitude));

      // 1. Handle Historical Path (Solid Blue)
      if (!paths.value.has(flightId)) {
          console.log(`[Map] Creating new path for ${flightId}`);
          const polyline = L.polyline([], {
              color: '#3B82F6', // Blue-500
              weight: 4,
              opacity: 0.8,
              smoothFactor: 0.5
          }).addTo(currentMap as L.Map);
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

      // 2. Handle Projected Path (Dashed Blue)
      if (!projectedPaths.value.has(flightId)) {
          const projPolyline = L.polyline([], {
              color: '#60A5FA', // Blue-400
              weight: 3,
              dashArray: '10, 15', 
              opacity: 0.9
          }).addTo(currentMap as L.Map);
          projectedPaths.value.set(flightId, markRaw(projPolyline));
      }

      const projPath = projectedPaths.value.get(flightId);
      if (projPath) {
          // Draw line from Current -> Target
          // Only update if target is different or significant distance
          projPath.setLatLngs([[lat, lng], [targetLat, targetLng]]);
      }

      if (markers.value.has(flightId)) {
        // Update existing marker
        const marker = markers.value.get(flightId);
        if (marker) {
          marker.setLatLng([lat, lng]);
          // Popup removed
          
          // Update rotation and scale
          const iconImg = marker.getElement()?.querySelector('.uav-icon') as HTMLElement;
          if (iconImg) {
            iconImg.style.transform = `rotate(${heading}deg) scale(${scale})`;
          }
        }
      } else {
        // Create new marker
        const newMarker = L.marker([lat, lng], { icon: createUavIcon(heading, altitude) })
          .addTo(currentMap as any);
          // Popup removed
        markers.value.set(flightId, newMarker);
      }
    });
  }
});

onUnmounted(async () => {
  await signalRService.stopConnection();
});
</script>

<template>
  <div class="map-wrapper">
    <div ref="mapContainer" class="map-container"></div>
    <FlightDataOverlay 
      v-if="currentFlightData"
      v-bind="currentFlightData"
    />
    <MissionChat />
  </div>
</template>

<style scoped>
.map-wrapper {
  position: relative;
  height: 100vh;
  width: 100%;
}
.map-container {
  height: 100%;
  width: 100%;
  z-index: 1;
}
</style>
