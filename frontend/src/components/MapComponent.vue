<script setup lang="ts">
import { onMounted, onUnmounted, ref, shallowRef } from 'vue';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';

import { setupLeafletPatches } from '../utils/leafletSetup';
import { useFlightVisualization } from '../composables/useFlightLayer';
import { useC4ILayer } from '../composables/useC4ILayer';

import MainLayout from './MainLayout.vue';
import FlightDataOverlay from './FlightDataOverlay.vue';
import MissionChat from './MissionChat.vue';
import NoFlyZoneModal from './NoFlyZoneModal.vue';
import PointModal from './PointModal.vue';

const mapContainer = ref<HTMLElement | null>(null);
const map = shallowRef<L.Map | null>(null);
let resizeObserver: ResizeObserver | null = null;

// Composables
const { 
    currentFlightData, 
    initializeFlightListeners, 
    stopFlightListeners 
} = useFlightVisualization(map);

const { 
    zones,
    points,
    deleteEntity,
    initializeRealtimeUpdates,
    showNewZoneModal, 
    newZoneForm,
    showPointModal,
    newPointForm,
    initializeDrawControls, 
    loadNoFlyZones,
    loadPoints,
    handleSaveZone, 
    handleCancelZone, 
    handleDeleteZone,
    handleSavePoint,
    handleCancelPoint,
    startDrawing,
    toggleEditMode,
    saveEdits,
    cancelEdits,
    isEditing
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

    // Light Matter Tiles (Voyager)
    L.tileLayer('https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png', {
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>',
      subdomains: 'abcd',
      maxZoom: 20
    }).addTo(leafletMap);

    // 3. Initialize Layers & Controls
    initializeDrawControls(leafletMap);
    await loadNoFlyZones();
    await loadPoints();
    initializeFlightListeners();
    initializeRealtimeUpdates();

    // 4. Handle resizing (Sidebar/Window)
    resizeObserver = new ResizeObserver(() => {
        leafletMap.invalidateSize();
    });
    resizeObserver.observe(mapContainer.value);
  }
});

onUnmounted(async () => {
  if (resizeObserver) resizeObserver.disconnect();
  await stopFlightListeners();
});
</script>

<template>
  <MainLayout 
    :is-editing="isEditing"
    :points="points"
    :zones="zones"
    @create-point="startDrawing('marker')"
    @create-zone="startDrawing('polygon')"
    @create-rectangle="startDrawing('rectangle')"
    @toggle-edit="toggleEditMode"
    @save-edits="saveEdits"
    @cancel-edits="cancelEdits"
    @delete-entity="deleteEntity"
  >
    <template #map>
      <div ref="mapContainer" class="h-full w-full"></div>
    </template>

    <template #telemetry>
        <FlightDataOverlay 
          v-if="currentFlightData"
          v-bind="currentFlightData"
        />
    </template>

    <template #chat>
        <MissionChat />
    </template>
  </MainLayout>

  <NoFlyZoneModal
    v-model:visible="showNewZoneModal"
    v-model:form="newZoneForm"
    @save="handleSaveZone"
    @cancel="handleCancelZone"
    @delete="handleDeleteZone"
  />

  <PointModal
    v-model:visible="showPointModal"
    v-model:form="newPointForm"
    @save="handleSavePoint"
    @cancel="handleCancelPoint"
  />
</template>

<style scoped>
/* Leaflet Draw overrides */
:deep(.leaflet-draw-toolbar a) {
  background-color: #1f2937; 
  border-color: #374151; 
  color: #e5e7eb; 
}
:deep(.leaflet-draw-toolbar a:hover) {
  background-color: #374151; 
}
:deep(.leaflet-bar) {
  border: none;
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.5);
}

/* Hide default Draw Toolbar since we use Sidebar */
:deep(.leaflet-draw) {
    display: none !important;
}

/* Push controls down to avoid Navbar overlap */
:deep(.leaflet-top.leaflet-left) {
  top: 80px;
}
</style>