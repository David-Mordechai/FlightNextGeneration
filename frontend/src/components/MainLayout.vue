<script setup lang="ts">
import type { Point, NoFlyZone } from '../services/C4IService';

defineProps<{ 
    isEditing: boolean;
    points: Point[];
    zones: NoFlyZone[];
}>();

defineEmits(['create-point', 'create-zone', 'create-rectangle', 'toggle-edit', 'save-edits', 'cancel-edits', 'delete-entity']);
</script>

<template>
  <div class="drawer h-screen w-screen">
    <input id="my-drawer" type="checkbox" class="drawer-toggle" />
    <div class="drawer-content relative h-full w-full overflow-hidden">
      <!-- 1. Map Layer (Background) -->
      <div class="absolute inset-0 z-0">
         <slot name="map"></slot>
      </div>

      <!-- 2. HUD Overlay (Foreground) -->
      <div class="relative z-10 flex flex-col h-full pointer-events-none">
        
        <!-- Top Bar (Glass) -->
        <div class="navbar bg-base-100/90 backdrop-blur-md text-base-content pointer-events-auto shadow-lg border-b border-white/10">
          <div class="flex-none">
            <label for="my-drawer" class="btn btn-square btn-ghost drawer-button text-primary">
              <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" class="inline-block w-6 h-6 stroke-current"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16"></path></svg>
            </label>
          </div>
          <div class="flex-1 px-2 mx-2">
            <div class="flex items-center gap-3">
                <img src="/Orbiter3.png" class="w-8 h-8 object-contain brightness-150" />
                <div class="flex flex-col">
                    <span class="text-xl font-black tracking-[0.2em] text-primary">SKYLAB</span>
                    <span class="text-[0.6rem] font-mono text-accent tracking-widest uppercase opacity-80">Mission Control System</span>
                </div>
            </div>
          </div>
        </div>

        <!-- Middle Area (Spacer for Map interaction) -->
        <div class="flex-1 pointer-events-none relative p-4">
            <!-- Telemetry Widget (Right Top) -->
            <div class="absolute top-4 right-4 pointer-events-auto z-20">
                <slot name="telemetry"></slot>
            </div>
            
             <!-- Chat Widget (Bottom Right) -->
            <div class="absolute bottom-4 right-4 w-96 max-h-[60vh] pointer-events-auto z-20 flex flex-col justify-end">
                <slot name="chat"></slot>
            </div>
        </div>

      </div>
    </div> 
    
    <!-- Sidebar Content -->
    <div class="drawer-side z-50">
      <label for="my-drawer" class="drawer-overlay"></label>
      <ul class="menu p-4 w-80 h-full bg-base-100/95 backdrop-blur-xl text-base-content border-r border-white/10 shadow-2xl">
        <!-- Sidebar Header -->
        <li class="mb-6">
            <div class="flex items-center w-full !bg-transparent p-0 -ml-2">
                <label for="my-drawer" class="btn btn-square btn-ghost text-primary drawer-button">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-6 h-6"><path stroke-linecap="round" stroke-linejoin="round" d="M18.75 19.5l-7.5-7.5 7.5-7.5m-6 15L5.25 12l7.5-7.5" /></svg>
                </label>
                <div class="flex flex-col ml-2">
                    <span class="font-bold text-xl tracking-wider text-primary">COMMAND</span>
                    <span class="text-[0.6rem] opacity-50 font-mono tracking-widest uppercase">Mission Operations</span>
                </div>
            </div>
        </li>
        
        <div class="divider my-0"></div>
        
        <li class="menu-title mt-4 text-accent/70 uppercase text-xs font-bold tracking-widest">Entity Management</li>
        
        <template v-if="!isEditing">
            <li><a @click="$emit('create-point')" class="hover:text-primary gap-3">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-6 h-6"><path stroke-linecap="round" stroke-linejoin="round" d="M15 10.5a3 3 0 11-6 0 3 3 0 016 0z" /><path stroke-linecap="round" stroke-linejoin="round" d="M19.5 10.5c0 7.142-7.5 11.25-7.5 11.25S4.5 17.642 4.5 10.5a7.5 7.5 0 1115 0z" /></svg>
                Create Point
            </a></li>
            <li><a @click="$emit('create-zone')" class="hover:text-error gap-3">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-6 h-6"><path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z" /></svg>
                Create Polygon Zone
            </a></li>
            <li><a @click="$emit('create-rectangle')" class="hover:text-error gap-3">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-6 h-6"><path stroke-linecap="round" stroke-linejoin="round" d="M5.25 7.5A2.25 2.25 0 017.5 5.25h9a2.25 2.25 0 012.25 2.25v9a2.25 2.25 0 01-2.25 2.25h-9a2.25 2.25 0 01-2.25-2.25v-9z" /></svg>
                Create Rectangle Zone
            </a></li>
            <li><a @click="$emit('toggle-edit')" class="hover:text-warning gap-3">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-6 h-6"><path stroke-linecap="round" stroke-linejoin="round" d="M16.862 4.487l1.687-1.688a1.875 1.875 0 112.652 2.652L10.582 16.07a4.5 4.5 0 01-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 011.13-1.897l8.932-8.931zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0115.75 21H5.25A2.25 2.25 0 013 18.75V8.25A2.25 2.25 0 015.25 6H10" /></svg>
                Edit Layers
            </a></li>
        </template>
        <template v-else>
            <li class="menu-title text-warning uppercase text-xs font-bold tracking-widest mt-2">Edit Mode Active</li>
            <li><a @click="$emit('save-edits')" class="text-success gap-3 font-bold bg-success/10 hover:bg-success/20">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-6 h-6"><path stroke-linecap="round" stroke-linejoin="round" d="M4.5 12.75l6 6 9-13.5" /></svg>
                Save Changes
            </a></li>
            <li><a @click="$emit('cancel-edits')" class="text-error gap-3 font-bold bg-error/10 hover:bg-error/20">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-6 h-6"><path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" /></svg>
                Cancel Edits
            </a></li>
        </template>

        <div class="divider my-4"></div>
        
        <!-- Scrollable Entity List Area -->
        <div class="flex-1 overflow-y-auto pr-1 custom-scrollbar">
            <li class="menu-title text-accent/70 uppercase text-xs font-bold tracking-widest sticky top-0 bg-base-100/95 backdrop-blur-md z-10 pb-2">Active Entities</li>

            <!-- Points -->
            <li v-for="point in points" :key="point.id">
                <div class="flex justify-between items-center group py-1">
                    <span class="flex items-center gap-2 text-sm">
                        <span class="text-lg">{{ point.type === 0 ? '🏠' : '🎯' }}</span>
                        {{ point.name }}
                    </span>
                    <button @click.stop="$emit('delete-entity', point.id, true)" class="btn btn-ghost btn-xs text-error opacity-0 group-hover:opacity-100" title="Delete Point">
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="w-4 h-4">
                            <path fill-rule="evenodd" d="M8.75 1A2.75 2.75 0 006 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 10.23 1.482l.149-.022.841 10.518A2.75 2.75 0 007.596 19h4.807a2.75 2.75 0 002.742-2.53l.841-10.52.149.023a.75.75 0 00.23-1.482A41.03 41.03 0 0014 4.193V3.75A2.75 2.75 0 0011.25 1h-2.5zM10 4c.84 0 1.673.025 2.5.075V3.75c0-.69-.56-1.25-1.25-1.25h-2.5c-.69 0-1.25.56-1.25 1.25v.325C8.327 4.025 9.16 4 10 4zM8.58 7.72a.75.75 0 00-1.5.06l.3 7.5a.75.75 0 101.5-.06l-.3-7.5zm4.34.06a.75.75 0 10-1.5-.06l-.3 7.5a.75.75 0 101.5.06l.3-7.5z" clip-rule="evenodd" />
                        </svg>
                    </button>
                </div>
            </li>

            <!-- Zones -->
            <li v-for="zone in zones" :key="zone.id">
                <div class="flex justify-between items-center group py-1">
                    <span class="flex items-center gap-2 text-sm">
                        <span class="text-lg">🛡️</span>
                        {{ zone.name }}
                    </span>
                    <button @click.stop="$emit('delete-entity', zone.id, false)" class="btn btn-ghost btn-xs text-error opacity-0 group-hover:opacity-100" title="Delete Zone">
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="w-4 h-4">
                            <path fill-rule="evenodd" d="M8.75 1A2.75 2.75 0 006 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 10.23 1.482l.149-.022.841 10.518A2.75 2.75 0 007.596 19h4.807a2.75 2.75 0 002.742-2.53l.841-10.52.149.023a.75.75 0 00.23-1.482A41.03 41.03 0 0014 4.193V3.75A2.75 2.75 0 0011.25 1h-2.5zM10 4c.84 0 1.673.025 2.5.075V3.75c0-.69-.56-1.25-1.25-1.25h-2.5c-.69 0-1.25.56-1.25 1.25v.325C8.327 4.025 9.16 4 10 4zM8.58 7.72a.75.75 0 00-1.5.06l.3 7.5a.75.75 0 101.5-.06l-.3-7.5zm4.34.06a.75.75 0 10-1.5-.06l-.3 7.5a.75.75 0 101.5.06l.3-7.5z" clip-rule="evenodd" />
                        </svg>
                    </button>
                </div>
            </li>
        </div>
      </ul>
    </div>
  </div>
</template>

<style scoped>
/* Custom Scrollbar for Sidebar List */
.custom-scrollbar::-webkit-scrollbar {
  width: 4px;
}
.custom-scrollbar::-webkit-scrollbar-track {
  background: rgba(255, 255, 255, 0.05);
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background: rgba(0, 255, 255, 0.2);
  border-radius: 10px;
}
.custom-scrollbar::-webkit-scrollbar-thumb:hover {
  background: rgba(0, 255, 255, 0.5);
}

/* Fix Sidebar Interaction */
.drawer-side {
  pointer-events: none !important;
}
.drawer-side .menu {
  pointer-events: auto !important;
}
.drawer-overlay {
  display: none !important;
}
</style>