<script setup lang="ts">
import CesiumMap from './components/CesiumMap.vue';
import { signalRService } from './services/SignalRService';
import { onMounted } from 'vue';

onMounted(async () => {
  await signalRService.startConnection();
});
</script>

<template>
  <CesiumMap />
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