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
  <div class="flex items-center justify-between gap-6 text-white pointer-events-none">
    
    <!-- Altitude -->
    <div class="flex flex-col gap-0">
      <span class="text-[8px] font-black uppercase tracking-widest text-primary">ALT</span>
      <div class="flex items-baseline">
        <span class="text-xl font-mono font-black">{{ Math.round(altitude) }}</span>
        <span class="text-[8px] opacity-40 ml-1">FT</span>
      </div>
    </div>

    <!-- Speed -->
    <div class="flex flex-col gap-0">
      <span class="text-[8px] font-black uppercase tracking-widest text-accent">SPD</span>
      <div class="flex items-baseline">
        <span class="text-xl font-mono font-black">{{ Math.round(speed) }}</span>
        <span class="text-[8px] opacity-40 ml-1">KTS</span>
      </div>
    </div>

    <!-- Heading -->
    <div class="flex flex-col gap-0">
      <span class="text-[8px] font-black uppercase tracking-widest text-white/40">HDG</span>
      <span class="text-xl font-mono font-black">{{ Math.round(heading) }}°</span>
    </div>

    <!-- Position -->
    <div class="flex-1 flex flex-col gap-0 text-right">
      <span class="text-[8px] font-black uppercase tracking-widest text-white/40">POS (LL)</span>
      <div class="flex gap-2 justify-end font-mono text-[10px] font-bold text-white/60">
        <span>{{ lat.toFixed(4) }}°N</span>
        <span>{{ lng.toFixed(4) }}°E</span>
      </div>
    </div>

  </div>
</template>
