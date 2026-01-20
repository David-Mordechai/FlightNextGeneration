<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import * as Cesium from 'cesium';

const props = defineProps<{
  lat: number;
  lng: number;
  altitude: number;
  pitch: number;
  yaw: number;
  flightId: string;
  // Main viewer reference to share resources
  mainViewer: Cesium.Viewer | null;
}>();

const payloadContainer = ref<HTMLElement | null>(null);
let pipViewer: Cesium.Viewer | null = null;
const FEET_TO_METERS = 0.3048;

onMounted(async () => {
  if (payloadContainer.value) {
    // Create a lightweight secondary viewer for the PiP
    pipViewer = new Cesium.Viewer(payloadContainer.value, {
      terrainProvider: new Cesium.EllipsoidTerrainProvider(), // Fast initial load
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
    });

    // Load actual terrain in background
    Cesium.createWorldTerrainAsync().then(provider => {
        if (pipViewer) pipViewer.terrainProvider = provider;
    });

    // Match imagery with main map
    const esri = await Cesium.ArcGisMapServerImageryProvider.fromUrl(
        'https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer'
    );
    pipViewer.imageryLayers.addImageryProvider(esri);

    // Disable heavy effects
    pipViewer.scene.fog.enabled = false;
    pipViewer.scene.globe.showGroundAtmosphere = false;
    if (pipViewer.scene.skyAtmosphere) {
      pipViewer.scene.skyAtmosphere.show = false;
    }
    
    // Hide UI elements
    (pipViewer.cesiumWidget.creditContainer as HTMLElement).style.display = 'none';
    
    // Initial camera placement
    updateCamera();
  }
});

const updateCamera = () => {
  if (!pipViewer) return;

  const position = Cesium.Cartesian3.fromDegrees(props.lng, props.lat, props.altitude * FEET_TO_METERS);
  
  pipViewer.camera.setView({
    destination: position,
    orientation: {
      heading: Cesium.Math.toRadians(props.yaw),
      pitch: Cesium.Math.toRadians(props.pitch),
      roll: 0.0
    }
  });
  
  // Force a render tick for the secondary view
  pipViewer.render();
};

// Update PiP camera based on telemetry
watch(() => [props.lat, props.lng, props.altitude, props.pitch, props.yaw], () => {
  updateCamera();
});

const formattedCoords = computed(() => {
  return `${props.lat.toFixed(6)}°N, ${props.lng.toFixed(6)}°E`;
});

const timestamp = computed(() => {
  const iso = new Date().toISOString();
  const timePart = iso.split('T')[1];
  return timePart ? timePart.split('.')[0] : '--:--:--';
});
</script>

<template>
  <div class="sensor-feed-container border-2 border-white/20 rounded-lg overflow-hidden shadow-2xl bg-black relative">
    <!-- Real Cesium Video Feed -->
    <div ref="payloadContainer" class="absolute inset-0 w-full h-full"></div>
    
    <!-- Video Static Effect -->
    <div class="absolute inset-0 noise-overlay opacity-5 pointer-events-none"></div>

    <!-- HUD Overlay -->
    <div class="absolute inset-0 p-4 flex flex-col justify-between font-mono text-[10px] text-lime-400 pointer-events-none uppercase tracking-wider z-10">
      
      <!-- Top HUD -->
      <div class="flex justify-between items-start">
        <div class="flex flex-col gap-1">
          <div class="flex items-center gap-2">
            <span class="w-2 h-2 bg-red-600 rounded-full animate-pulse"></span>
            <span>REC [LIVE]</span>
          </div>
          <span>{{ flightId }}</span>
          <span>SENS: EO/IR-V2</span>
        </div>
        <div class="text-right">
          <span>{{ timestamp }} UTC</span>
          <br/>
          <span>ALT: {{ Math.round(altitude) }} FT</span>
        </div>
      </div>

      <!-- Center Crosshair -->
      <div class="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-32 h-32 opacity-60">
        <!-- Brackets -->
        <div class="absolute top-0 left-0 w-4 h-4 border-t-2 border-l-2 border-lime-400"></div>
        <div class="absolute top-0 right-0 w-4 h-4 border-t-2 border-r-2 border-lime-400"></div>
        <div class="absolute bottom-0 left-0 w-4 h-4 border-b-2 border-l-2 border-lime-400"></div>
        <div class="absolute bottom-0 right-0 w-4 h-4 border-b-2 border-r-2 border-lime-400"></div>
        
        <!-- Center Dot -->
        <div class="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-1 h-1 bg-lime-400 rounded-full"></div>
        
        <!-- Dynamic Yaw Scale -->
        <div class="absolute -bottom-8 left-1/2 -translate-x-1/2 flex flex-col items-center">
            <span>HDG: {{ Math.round((yaw + 360) % 360) }}°</span>
            <div class="w-24 h-1 border-b border-lime-400/50 mt-1 relative">
                <div class="absolute top-0 h-2 w-0.5 bg-lime-400 left-1/2"></div>
            </div>
        </div>
      </div>

      <!-- Bottom HUD -->
      <div class="flex justify-between items-end">
        <div class="flex flex-col gap-1">
          <span>COORDS: {{ formattedCoords }}</span>
          <div class="flex gap-4">
            <span>PITCH: {{ pitch.toFixed(1) }}°</span>
            <span>ROLL: 0.0°</span>
          </div>
        </div>
        <div class="text-right">
          <div class="flex flex-col items-end">
            <span class="text-xs font-bold">FOV: 15.0°</span>
            <span>ZOOM: 4.0X</span>
          </div>
        </div>
      </div>

    </div>

    <!-- Vignette Effect -->
    <div class="absolute inset-0 pointer-events-none shadow-[inset_0_0_100px_rgba(0,0,0,0.8)] z-20"></div>
  </div>
</template>

<style scoped>
.sensor-feed-container {
  aspect-ratio: 16 / 9;
  width: 100%;
}

:deep(.cesium-viewer-bottom) {
    display: none !important;
}

.noise-overlay {
  background-image: url("data:image/svg+xml,%3Csvg viewBox='0 0 200 200' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='noiseFilter'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.65' numOctaves='3' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23noiseFilter)'/%3E%3C/svg%3E");
}

/* Green Color Grading for the whole container */
.sensor-feed-container::after {
  content: '';
  position: absolute;
  inset: 0;
  background: rgba(20, 255, 20, 0.05);
  pointer-events: none;
  z-index: 30;
}
</style>

<style scoped>
.sensor-feed-container {
  aspect-ratio: 16 / 9;
  width: 100%;
}

.noise-overlay {
  background-image: url("data:image/svg+xml,%3Csvg viewBox='0 0 200 200' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='noiseFilter'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.65' numOctaves='3' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23noiseFilter)'/%3E%3C/svg%3E");
}

/* Green Color Grading for the whole container */
.sensor-feed-container::after {
  content: '';
  position: absolute;
  inset: 0;
  background: rgba(20, 255, 20, 0.05);
  pointer-events: none;
}
</style>
