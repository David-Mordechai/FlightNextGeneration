<script setup lang="ts">
import { onMounted, onUnmounted, ref, shallowRef, type ShallowRef } from 'vue';
import * as Cesium from 'cesium';
import "cesium/Build/Cesium/Widgets/widgets.css";

import { useCesiumFlightVisualization } from '../composables/useCesiumFlightLayer';
import { useCesiumC4ILayer } from '../composables/useCesiumC4ILayer';

import MainLayout from './MainLayout.vue';
import FlightDataOverlay from './FlightDataOverlay.vue';
import MissionChat from './MissionChat.vue';
import NoFlyZoneModal from './NoFlyZoneModal.vue';
import PointModal from './PointModal.vue';

const mapContainer = ref<HTMLElement | null>(null);
const viewer = shallowRef<Cesium.Viewer | null>(null);

// Composables
const { 
    currentFlightData, 
    initializeFlightListeners, 
    stopFlightListeners 
} = useCesiumFlightVisualization(viewer as ShallowRef<Cesium.Viewer | null>);

const { 
    zones,
    points,
    deleteEntity,
    initializeRealtimeUpdates,
    showNewZoneModal, 
    newZoneForm,
    showPointModal,
    newPointForm,
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
} = useCesiumC4ILayer(viewer as ShallowRef<Cesium.Viewer | null>);

onMounted(async () => {
  if (mapContainer.value) {
    const viewerInstance = new Cesium.Viewer(mapContainer.value, {
      terrainProvider: await Cesium.createWorldTerrainAsync(),
      animation: false,
      baseLayerPicker: false,
      fullscreenButton: false,
      geocoder: false,
      homeButton: false,
      infoBox: false,
      sceneModePicker: false,
      selectionIndicator: false,
      timeline: false,
      navigationHelpButton: false,
      scene3DOnly: true,
    });

    // Add high-res satellite imagery
    const esri = await Cesium.ArcGisMapServerImageryProvider.fromUrl(
        'https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer'
    );
    viewerInstance.imageryLayers.addImageryProvider(esri);

    // Set starting view to Ashdod
    viewerInstance.camera.setView({
        destination: Cesium.Cartesian3.fromDegrees(34.65, 31.80, 15000),
        orientation: {
            heading: Cesium.Math.toRadians(0),
            pitch: Cesium.Math.toRadians(-45),
            roll: 0.0
        }
    });

    viewer.value = viewerInstance;

    await loadNoFlyZones();
    await loadPoints();
    initializeFlightListeners();
    initializeRealtimeUpdates();
  }
});

onUnmounted(async () => {
  await stopFlightListeners();
  if (viewer.value) {
    viewer.value.destroy();
  }
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
      <div ref="mapContainer" class="h-full w-full cesium-container"></div>
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
.cesium-container {
    background: #000;
}

:deep(.cesium-viewer-bottom) {
    display: none;
}
</style>
