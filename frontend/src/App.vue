<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue';
import MapComponent from './components/MapComponent.vue'
import { signalRService } from './services/SignalRService';

onMounted(async () => {
  // Start SignalR connection once at app level
  await signalRService.startConnection();
});

onUnmounted(async () => {
  // Stop connection when app unmounts
  await signalRService.stopConnection();
});
</script>

<template>
  <div class="app-container">
    <MapComponent />
  </div>
</template>

<style>
body, html, #app {
  margin: 0;
  padding: 0;
  height: 100%;
  width: 100%;
}

.app-container {
  height: 100vh;
  width: 100vw;
}
</style>