<script setup lang="ts">
defineProps<{
  flightId: string;
  lat: number;
  lng: number;
  altitude: number;
  speed: number;
  heading: number;
}>();
</script>

<template>
  <div class="card w-64 bg-base-100/70 backdrop-blur-md shadow-xl border border-white/10">
    <div class="card-body p-4 gap-1">
      <div class="flex items-center justify-between mb-2">
          <h2 class="card-title text-xs font-mono font-bold tracking-widest text-primary">{{ flightId }}</h2>
      </div>
      
      <!-- Stats Grid -->
      <div class="grid grid-cols-2 gap-4">
          <!-- Altitude -->
          <div class="flex flex-col">
              <span class="text-[10px] uppercase font-bold text-info tracking-wider opacity-70">ALT (ft)</span>
              <span class="text-xl font-mono font-bold">{{ Math.round(altitude) }}</span>
              <progress class="progress progress-info w-full h-1 mt-1" :value="altitude" max="10000"></progress>
          </div>
          
          <!-- Speed -->
          <div class="flex flex-col">
              <span class="text-[10px] uppercase font-bold text-success tracking-wider opacity-70">SPD (kts)</span>
              <span class="text-xl font-mono font-bold">{{ Math.round(speed) }}</span>
              <progress class="progress progress-success w-full h-1 mt-1" :value="speed" max="300"></progress>
          </div>
      </div>

      <div class="divider my-2 opacity-50"></div>

      <!-- Heading -->
      <div class="flex items-center justify-between">
          <span class="text-[10px] uppercase font-bold text-warning tracking-wider">HDG</span>
          <span class="font-mono text-lg font-bold text-warning">{{ Math.round(heading) }}°</span>
      </div>
      
      <!-- Coordinates -->
      <div class="mt-2 text-[10px] font-mono opacity-50 bg-black/20 p-1 rounded text-center">
          {{ lat.toFixed(5) }}, {{ lng.toFixed(5) }}
      </div>
    </div>
  </div>
</template>
