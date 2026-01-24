<script setup lang="ts">
import { ref, computed, watch } from 'vue';

const props = defineProps<{
  lat: number;
  lng: number;
  altitude: number;
  pitch: number;
  yaw: number;
  flightId: string;
  fov: number; // Received from parent
}>();

const emit = defineEmits(['update:fov']);

const iframeRef = ref<HTMLIFrameElement | null>(null);

// Send telemetry updates to the isolated iframe
watch(() => [props.lat, props.lng, props.altitude, props.pitch, props.yaw], () => {
    if (iframeRef.value && iframeRef.value.contentWindow) {
        iframeRef.value.contentWindow.postMessage({
            type: 'UPDATE_CAMERA',
            payload: {
                lat: props.lat,
                lng: props.lng,
                altitude: props.altitude,
                pitch: props.pitch,
                yaw: props.yaw
            }
        }, '*');
    }
});

// Send FOV updates
watch(() => props.fov, (newFov) => {
    if (iframeRef.value && iframeRef.value.contentWindow) {
        iframeRef.value.contentWindow.postMessage({
            type: 'UPDATE_FOV',
            payload: newFov
        }, '*');
    }
});

const setZoom = (fov: number) => {
    emit('update:fov', fov);
};

const formattedCoords = computed(() => {
  return `${props.lat.toFixed(6)}°N, ${props.lng.toFixed(6)}°E`;
});

const timestamp = computed(() => {
  const parts = new Date().toISOString().split('T');
  return (parts.length > 1 && parts[1]) ? parts[1].split('.')[0] : '00:00:00';
});

const zoomLevelLabel = computed(() => {
    if (props.fov >= 20) return 'WIDE';
    if (props.fov >= 10) return 'MED';
    if (props.fov >= 5) return 'NARROW';
    return 'ULTRA';
});

</script>

<template>
  <div class="sensor-feed-container overflow-hidden bg-black relative">
    <!-- Isolated Cesium Video Feed via IFrame -->
    <iframe 
        ref="iframeRef" 
        src="/pip.html" 
        class="absolute inset-0 w-full h-full border-none"
    ></iframe>
    
    <!-- Video Static Effect -->
    <div class="absolute inset-0 noise-overlay opacity-5 pointer-events-none"></div>

    <!-- HUD Overlay -->
    <div class="absolute inset-0 p-4 flex flex-col justify-between font-mono text-[10px] text-yellow-400 pointer-events-none uppercase tracking-wider z-10 drop-shadow-[0_1px_2px_rgba(0,0,0,0.8)]">
      
      <!-- Top HUD -->
      <div class="flex justify-between items-start">
        <div class="flex flex-col gap-1">
          <div class="flex items-center gap-2">
            <span class="w-2 h-2 bg-red-600 rounded-full animate-pulse shadow-[0_0_5px_red]"></span>
            <span class="font-bold">REC [LIVE]</span>
          </div>
          <span class="font-bold">{{ flightId }}</span>
          <span class="opacity-80">SENS: EO/IR-V2</span>
        </div>
        <div class="text-right font-bold">
          <span>{{ timestamp }} UTC</span>
          <br/>
          <span>ALT: {{ Math.round(altitude) }} FT</span>
        </div>
      </div>

      <!-- Center Crosshair -->
      <div class="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-32 h-32 opacity-80">
        <!-- Brackets -->
        <div class="absolute top-0 left-0 w-4 h-4 border-t-2 border-l-2 border-yellow-400"></div>
        <div class="absolute top-0 right-0 w-4 h-4 border-t-2 border-r-2 border-yellow-400"></div>
        <div class="absolute bottom-0 left-0 w-4 h-4 border-b-2 border-l-2 border-yellow-400"></div>
        <div class="absolute bottom-0 right-0 w-4 h-4 border-b-2 border-r-2 border-yellow-400"></div>
        
        <!-- Center Dot -->
        <div class="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-1 h-1 bg-yellow-400 rounded-full"></div>
        
        <!-- Dynamic Yaw Scale -->
        <div class="absolute -bottom-8 left-1/2 -translate-x-1/2 flex flex-col items-center">
            <span class="font-bold">HDG: {{ Math.round((yaw + 360) % 360) }}°</span>
            <div class="w-24 h-1 border-b border-yellow-400/50 mt-1 relative">
                <div class="absolute top-0 h-2 w-0.5 bg-yellow-400 left-1/2"></div>
            </div>
        </div>
      </div>

      <!-- Bottom HUD -->
      <div class="flex justify-between items-end font-bold">
        <div class="flex flex-col gap-1">
          <span>COORDS: {{ formattedCoords }}</span>
          <div class="flex gap-4">
            <span>PITCH: {{ pitch.toFixed(1) }}°</span>
            <span>ROLL: 0.0°</span>
          </div>
        </div>
        <div class="text-right">
          <div class="flex flex-col items-end gap-1">
            <span class="text-xs font-black">FOV: {{ fov.toFixed(1) }}° [{{ zoomLevelLabel }}]</span>
            <!-- Zoom Controls (Pointer Events Enabled) -->
            <div class="flex bg-black/50 border border-yellow-400/30 rounded pointer-events-auto mt-1">
                <button @click="setZoom(20)" class="px-2 py-1 hover:bg-yellow-400/20" :class="{ 'bg-yellow-400 text-black': fov === 20 }">W</button>
                <div class="w-px bg-yellow-400/30"></div>
                <button @click="setZoom(10)" class="px-2 py-1 hover:bg-yellow-400/20" :class="{ 'bg-yellow-400 text-black': fov === 10 }">M</button>
                <div class="w-px bg-yellow-400/30"></div>
                <button @click="setZoom(5)" class="px-2 py-1 hover:bg-yellow-400/20" :class="{ 'bg-yellow-400 text-black': fov === 5 }">N</button>
                <div class="w-px bg-yellow-400/30"></div>
                <button @click="setZoom(1)" class="px-2 py-1 hover:bg-yellow-400/20" :class="{ 'bg-yellow-400 text-black': fov === 1 }">U</button>
            </div>
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
