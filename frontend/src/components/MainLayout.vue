<script setup lang="ts">
import { ref } from 'vue';
import type { Point, NoFlyZone } from '../services/C4IService';

defineProps<{ 
    isEditing: boolean;
    points: Point[];
    zones: NoFlyZone[];
}>();

defineEmits(['create-point', 'create-zone', 'create-rectangle', 'toggle-edit', 'save-edits', 'cancel-edits', 'delete-entity']);

const isSidebarOpen = ref(true);
</script>

<template>
  <div class="flex h-screen w-screen overflow-hidden bg-black">
    
    <!-- 1. PERSISTENT SIDEBAR (No Overlay) -->
    <aside 
      class="h-full bg-base-100 border-r border-white/10 shadow-2xl transition-all duration-300 ease-in-out z-30 flex flex-col"
      :class="isSidebarOpen ? 'w-80' : 'w-0'"
    >
      <div class="w-80 h-full p-4 flex flex-col overflow-y-auto custom-scrollbar" v-if="isSidebarOpen">
        <!-- Sidebar Header -->
        <div class="flex items-center mb-6">
            <button @click="isSidebarOpen = false" class="btn btn-square btn-ghost text-primary mr-2">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-6 h-6"><path stroke-linecap="round" stroke-linejoin="round" d="M18.75 19.5l-7.5-7.5 7.5-7.5m-6 15L5.25 12l7.5-7.5" /></svg>
            </button>
            <div class="flex flex-col">
                <span class="font-bold text-xl tracking-wider text-primary">COMMAND</span>
                <span class="text-[0.6rem] opacity-50 font-mono tracking-widest uppercase">Mission Operations</span>
            </div>
        </div>
        
        <div class="divider my-0"></div>
        
        <div class="menu-title mt-4 text-accent/70 uppercase text-xs font-bold tracking-widest">Entity Management</div>
        
        <ul class="menu p-0 mt-2 gap-1">
            <template v-if="!isEditing">
                <li><a @click="$emit('create-point')" class="hover:text-primary gap-3 py-3">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-6 h-6"><path stroke-linecap="round" stroke-linejoin="round" d="M15 10.5a3 3 0 11-6 0 3 3 0 016 0z" /><path stroke-linecap="round" stroke-linejoin="round" d="M19.5 10.5c0 7.142-7.5 11.25-7.5 11.25S4.5 17.642 4.5 10.5a7.5 7.5 0 1115 0z" /></svg>
                    Create Point
                </a></li>
                <li><a @click="$emit('create-zone')" class="hover:text-error gap-3 py-3">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-6 h-6"><path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z" /></svg>
                    Create Polygon Zone
                </a></li>
                <li><a @click="$emit('create-rectangle')" class="hover:text-error gap-3 py-3">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-6 h-6"><path stroke-linecap="round" stroke-linejoin="round" d="M5.25 7.5A2.25 2.25 0 017.5 5.25h9a2.25 2.25 0 012.25 2.25v9a2.25 2.25 0 01-2.25 2.25h-9a2.25 2.25 0 01-2.25-2.25v-9z" /></svg>
                    Create Rectangle Zone
                </a></li>
                <li><a @click="$emit('toggle-edit')" class="hover:text-warning gap-3 py-3">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-6 h-6"><path stroke-linecap="round" stroke-linejoin="round" d="M16.862 4.487l1.687-1.688a1.875 1.875 0 112.652 2.652L10.582 16.07a4.5 4.5 0 01-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 011.13-1.897l8.932-8.931zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0115.75 21H5.25A2.25 2.25 0 013 18.75V8.25A2.25 2.25 0 015.25 6H10" /></svg>
                    Edit Layers
                </a></li>
            </template>
            <template v-else>
                <div class="text-warning uppercase text-xs font-bold tracking-widest mt-2 mb-2">Edit Mode Active</div>
                <li><a @click="$emit('save-edits')" class="text-success gap-3 font-bold bg-success/10 hover:bg-success/20 py-3 mb-1">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-6 h-6"><path stroke-linecap="round" stroke-linejoin="round" d="M4.5 12.75l6 6 9-13.5" /></svg>
                    Save Changes
                </a></li>
                <li><a @click="$emit('cancel-edits')" class="text-error gap-3 font-bold bg-error/10 hover:bg-error/20 py-3">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-6 h-6"><path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" /></svg>
                    Cancel Edits
                </a></li>
            </template>
        </ul>

        <div class="divider my-4"></div>
        
        <!-- Active Entities List -->
        <div class="flex-1 overflow-y-auto">
            <div class="text-accent/70 uppercase text-xs font-bold tracking-widest mb-3">Active Entities</div>

            <ul class="menu p-0 gap-1">
                <!-- Points -->
                <li v-for="point in points" :key="point.id">
                    <div class="flex justify-between items-center group py-2">
                        <span class="flex items-center gap-2 text-sm">
                            <span class="text-lg">{{ point.type === 0 ? '🏠' : '🎯' }}</span>
                            {{ point.name }}
                        </span>
                        <button @click.stop="$emit('delete-entity', point.id, true)" class="btn btn-ghost btn-xs text-error opacity-0 group-hover:opacity-100">
                            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="w-4 h-4"><path fill-rule="evenodd" d="M8.75 1A2.75 2.75 0 006 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 10.23 1.482l.149-.022.841 10.518A2.75 2.75 0 007.596 19h4.807a2.75 2.75 0 002.742-2.53l.841-10.52.149.023a.75.75 0 00.23-1.482A41.03 41.03 0 0014 4.193V3.75A2.75 2.75 0 0011.25 1h-2.5zM10 4c.84 0 1.673.025 2.5.075V3.75c0-.69-.56-1.25-1.25-1.25h-2.5c-.69 0-1.25.56-1.25 1.25v.325C8.327 4.025 9.16 4 10 4zM8.58 7.72a.75.75 0 00-1.5.06l.3 7.5a.75.75 0 101.5-.06l-.3-7.5zm4.34.06a.75.75 0 10-1.5-.06l-.3 7.5a.75.75 0 101.5.06l.3-7.5z" clip-rule="evenodd" /></svg>
                        </button>
                    </div>
                </li>

                <!-- Zones -->
                <li v-for="zone in zones" :key="zone.id">
                    <div class="flex justify-between items-center group py-2">
                        <span class="flex items-center gap-2 text-sm">
                            <span class="text-lg">🛡️</span>
                            {{ zone.name }}
                        </span>
                        <button @click.stop="$emit('delete-entity', zone.id, false)" class="btn btn-ghost btn-xs text-error opacity-0 group-hover:opacity-100">
                            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="w-4 h-4"><path fill-rule="evenodd" d="M8.75 1A2.75 2.75 0 006 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 10.23 1.482l.149-.022.841 10.518A2.75 2.75 0 007.596 19h4.807a2.75 2.75 0 002.742-2.53l.841-10.52.149.023a.75.75 0 00.23-1.482A41.03 41.03 0 0014 4.193V3.75A2.75 2.75 0 0011.25 1h-2.5zM10 4c.84 0 1.673.025 2.5.075V3.75c0-.69-.56-1.25-1.25-1.25h-2.5c-.69 0-1.25.56-1.25 1.25v.325C8.327 4.025 9.16 4 10 4zM8.58 7.72a.75.75 0 00-1.5.06l.3 7.5a.75.75 0 101.5-.06l-.3-7.5zm4.34.06a.75.75 0 10-1.5-.06l-.3 7.5a.75.75 0 101.5.06l.3-7.5z" clip-rule="evenodd" /></svg>
                        </button>
                    </div>
                </li>
            </ul>
        </div>
      </div>
    </aside>

    <!-- 2. MAIN CONTENT (MAP + HUD) -->
    <main class="flex-1 relative flex flex-col overflow-hidden min-w-0">
      
      <!-- Top Navigation -->
      <nav class="bg-base-100/90 backdrop-blur-md border-b border-white/10 shadow-lg px-4 h-16 flex items-center z-20">
        <button v-if="!isSidebarOpen" @click="isSidebarOpen = true" class="btn btn-square btn-ghost text-primary mr-4">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" class="inline-block w-6 h-6 stroke-current"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16"></path></svg>
        </button>
        
        <div class="flex items-center gap-3">
            <img src="/Orbiter3.png" class="w-8 h-8 object-contain brightness-150" />
            <div class="flex flex-col">
                <span class="text-xl font-black tracking-[0.2em] text-primary leading-none">SKYLAB</span>
                <span class="text-[0.6rem] font-mono text-accent tracking-widest uppercase opacity-80 mt-1">Mission Control System</span>
            </div>
        </div>
      </nav>

      <!-- Map Area -->
      <div class="flex-1 relative z-0 flex overflow-hidden">
         <!-- Central Map Pane -->
         <div class="flex-1 relative">
            <slot name="map"></slot>
         </div>

         <!-- 3. RIGHT CONTROL PANEL (Triple Stack) -->
         <aside class="w-[450px] h-full bg-base-100 border-l border-white/10 flex flex-col overflow-hidden shadow-2xl z-20">
            
            <!-- Top Section: Video (Edge-to-Edge) -->
            <div class="relative border-b border-white/5 bg-black flex-none">
                <slot name="video"></slot>
            </div>

            <!-- Middle Section: Horizontal Telemetry Strip -->
            <div class="p-4 bg-base-200/50 border-b border-white/5 flex-none">
                <slot name="telemetry"></slot>
            </div>

            <!-- Bottom Section: Chat (Flex-1) -->
            <div class="flex-1 flex flex-col min-h-0 bg-base-100">
                <slot name="chat"></slot>
            </div>

         </aside>
      </div>
    </main>

  </div>
</template>

<style scoped>
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
</style>
