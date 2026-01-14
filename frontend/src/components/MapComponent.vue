<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';

import { setupLeafletPatches } from '../utils/leafletSetup';
import { useFlightVisualization } from '../composables/useFlightLayer';
import { useC4ILayer } from '../composables/useC4ILayer';

import FlightDataOverlay from './FlightDataOverlay.vue';
import MissionChat from './MissionChat.vue';
import NoFlyZoneModal from './NoFlyZoneModal.vue';

const mapContainer = ref<HTMLElement | null>(null);
const map = ref<L.Map | null>(null);

// Composables
const { 
    currentFlightData, 
    initializeFlightListeners, 
    stopFlightListeners 
} = useFlightVisualization(map);

const { 
    showNewZoneModal, 
    newZoneForm, 
    initializeDrawControls, 
    loadNoFlyZones, 
    handleSaveZone, 
    handleCancelZone, 
    handleDeleteZone 
} = useC4ILayer(map);

onMounted(async () => {
  if (mapContainer.value) {
    // 1. Setup Leaflet Patches
    await setupLeafletPatches();

    // 2. Initialize Map
    const leafletMap = L.map(mapContainer.value, {
      zoomControl: false 
    }).setView([31.0461, 34.8516], 8);
    
    L.control.zoom({ position: 'bottomright' }).addTo(leafletMap);
    map.value = leafletMap;

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    }).addTo(leafletMap);

    // 3. Initialize Layers & Controls
    initializeDrawControls(leafletMap);
    await loadNoFlyZones();
    initializeFlightListeners();
  }
});

onUnmounted(async () => {
  await stopFlightListeners();
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

    <NoFlyZoneModal
      v-model:visible="showNewZoneModal"
      v-model:form="newZoneForm"
      @save="handleSaveZone"
      @cancel="handleCancelZone"
      @delete="handleDeleteZone"
    />
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
