<script setup lang="ts">
import type { ScreenLabel } from '../composables/useScreenLabels';

defineProps<{
  labels: ScreenLabel[];
}>();
</script>

<template>
  <div class="absolute inset-0 pointer-events-none overflow-hidden z-20">
    <div 
      v-for="label in labels" 
      :key="label.id"
      class="absolute transform -translate-x-1/2 -translate-y-full transition-opacity duration-75 flex flex-col items-center"
      :style="{ left: `${label.x}px`, top: `${label.y}px`, opacity: label.visible ? 1 : 0 }"
    >
      <div class="glass-label px-3 py-1 rounded shadow-lg border border-white/20 flex items-center gap-2">
        <span class="w-2 h-2 rounded-full" :class="label.type === 'home' ? 'bg-blue-500' : label.type === 'target' ? 'bg-red-500' : label.type === 'uav' ? 'bg-cyan-400' : 'bg-transparent hidden'"></span>
        <div class="flex flex-col">
            <span class="text-xs font-medium text-white tracking-wide font-sans whitespace-nowrap">{{ label.name }}</span>
            <div v-if="label.subLabel" class="flex flex-col">
                <span v-for="(line, idx) in label.subLabel.split('|')" :key="idx" class="text-[10px] font-mono font-bold text-cyan-300 leading-none py-0.5 whitespace-nowrap">
                    {{ line }}
                </span>
            </div>
        </div>
      </div>
      <!-- Triangle Pointer -->
      <div class="w-0 h-0 border-l-[5px] border-l-transparent border-r-[5px] border-r-transparent border-t-[6px] border-t-slate-900/60 mx-auto"></div>
    </div>
  </div>
</template>

<style scoped>
.glass-label {
  background: rgba(15, 23, 42, 0.60); /* Slate 900 with 60% opacity */
  backdrop-filter: blur(4px);
  -webkit-backdrop-filter: blur(4px);
  text-shadow: 0 1px 2px rgba(0,0,0,0.5);
}
</style>
