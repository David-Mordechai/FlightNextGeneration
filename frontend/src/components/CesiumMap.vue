<script setup lang="ts">
import { onMounted, onUnmounted, ref, shallowRef, watch, type ShallowRef } from 'vue';
import * as Cesium from 'cesium';
import "cesium/Build/Cesium/Widgets/widgets.css";

import { useCesiumFlightVisualization } from '../composables/useCesiumFlightLayer';
import { useCesiumC4ILayer } from '../composables/useCesiumC4ILayer';
import { useScreenLabels } from '../composables/useScreenLabels'; // Import shared labels
import { useCesiumSimulatedEntities } from '../composables/useCesiumSimulatedEntities';

import MainLayout from './MainLayout.vue';
import FlightDataOverlay from './FlightDataOverlay.vue';
import MissionChat from './MissionChat.vue';
import NoFlyZoneModal from './NoFlyZoneModal.vue';
import PointModal from './PointModal.vue';
import EntityLabels from './EntityLabels.vue';
import SensorFeed from './SensorFeed.vue';

const mapContainer = ref<HTMLElement | null>(null);
const viewer = shallowRef<Cesium.Viewer | null>(null);

// Expose viewer for PiP sub-rendering
defineExpose({ viewer });

// Shared Labels
const { screenLabels, initializeLabelSystem, destroyLabelSystem } = useScreenLabels();

// Composables
const { 
    currentFlightData, 
    sensorFov,
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

const { 
    createSimulatedEntities
} = useCesiumSimulatedEntities();

const isSensorActive = ref(false);

// Update PiP camera based on telemetry
watch(() => currentFlightData.value, (newVal) => {
    if (newVal && !isSensorActive.value) {
        isSensorActive.value = true;
    }
});

onMounted(async () => {  if (mapContainer.value) {
    try {
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
          useBrowserRecommendedResolution: true,
          requestRenderMode: true, // Only render when scene changes
          maximumRenderTimeChange: 0.033, // Match 30fps max
          showRenderLoopErrors: false, // Disable the intrusive error popup
          contextOptions: {
            webgl: {
              failIfMajorPerformanceCaveat: false, // Don't fail if performance is low
            }
          }
        });

        // Disable heavy effects
        viewerInstance.scene.logarithmicDepthBuffer = false;
        viewerInstance.scene.highDynamicRange = false;
        viewerInstance.scene.postProcessStages.fxaa.enabled = false;
        viewerInstance.scene.fog.enabled = false; // Fog is expensive
        viewerInstance.scene.globe.showGroundAtmosphere = false; // Reduce shader complexity
        viewerInstance.shadows = false;
        if (viewerInstance.scene.shadowMap) viewerInstance.scene.shadowMap.enabled = false;
        const esri = await Cesium.ArcGisMapServerImageryProvider.fromUrl(
            'https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer'
        );
        viewerInstance.imageryLayers.addImageryProvider(esri);

        // Set starting view near the simulated entities (31.565, 34.54)
        viewerInstance.camera.setView({
            destination: Cesium.Cartesian3.fromDegrees(34.54, 31.56, 2000),
            orientation: {
                heading: Cesium.Math.toRadians(0),
                pitch: Cesium.Math.toRadians(-45),
                roll: 0.0
            }
        });

        // CRITICAL: Enable depth test for accurate picking against terrain
        viewerInstance.scene.globe.depthTestAgainstTerrain = true;

        // Suppress default Cesium error popup (rethrow instead)
        viewerInstance.scene.rethrowRenderErrors = true;

        viewer.value = viewerInstance;

        // Load all data
        await loadNoFlyZones();
        await loadPoints();
        createSimulatedEntities(viewerInstance, true, '?v=main');
        initializeFlightListeners();
        initializeRealtimeUpdates();
        
        // Initialize HTML Label System
        initializeLabelSystem(viewerInstance);
    } catch (error) {
        console.error("FATAL: CesiumMap initialization failed:", error);
    }
  }
});

onUnmounted(async () => {
  if (viewer.value) {
    destroyLabelSystem(viewer.value);
  }
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
      <EntityLabels :labels="screenLabels" />
    </template>

    <template #video>
        <SensorFeed 
          v-if="isSensorActive"
          :lat="currentFlightData!.lat"
          :lng="currentFlightData!.lng"
          :altitude="currentFlightData!.altitude"
          :pitch="currentFlightData!.payloadPitch"
          :yaw="currentFlightData!.payloadYaw"
          :flightId="currentFlightData!.flightId"
          :fov="sensorFov"
          @update:fov="(val) => sensorFov = val"
        />
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
