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
  <div class="p-5 bg-base-100/90 backdrop-blur-xl rounded-2xl border border-white/10 shadow-2xl w-60 text-slate-100">
    <!-- Primary Stats -->
    <div class="grid grid-cols-2 gap-4 mb-4">
      <div class="flex flex-col gap-1">
        <span class="text-[9px] font-bold uppercase tracking-widest text-white/40">Altitude</span>
        <div class="flex items-baseline">
          <span class="text-3xl font-mono font-black leading-none text-white">{{ Math.round(altitude) }}</span>
          <span class="text-[10px] text-white/20 ml-1 font-bold">FT</span>
        </div>
      </div>
      <div class="flex flex-col gap-1 text-right">
        <span class="text-[9px] font-bold uppercase tracking-widest text-white/40">Speed</span>
        <div class="flex items-baseline justify-end">
          <span class="text-3xl font-mono font-black leading-none text-white">{{ Math.round(speed) }}</span>
          <span class="text-[10px] text-white/20 ml-1 font-bold">KTS</span>
        </div>
      </div>
    </div>

    <!-- Footer: Heading and Coordinates -->
    <div class="flex items-center justify-between border-t border-white/5 pt-4">
      <div class="flex flex-col gap-0.5">
          <span class="text-[8px] font-bold uppercase tracking-widest text-white/40">Heading</span>
          <span class="text-xl font-mono font-black text-white leading-none">{{ Math.round(heading) }}°</span>
      </div>
      <div class="flex flex-col gap-0.5 text-right">
          <span class="text-[8px] font-bold uppercase tracking-widest text-white/40">Position</span>
          <div class="flex flex-col font-mono text-[11px] text-white/60 leading-tight">
            <span>{{ lat.toFixed(4) }}°N</span>
            <span>{{ lng.toFixed(4) }}°E</span>
          </div>
      </div>
    </div>
  </div>
</template>
