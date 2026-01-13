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
  <div class="flight-data-card">
    <div class="header">
      <div class="status-indicator"></div>
      <h3>{{ flightId }}</h3>
    </div>
    
    <div class="data-grid">
      <div class="data-item">
        <span class="label">COORDINATES</span>
        <span class="value">{{ lat.toFixed(5) }}, {{ lng.toFixed(5) }}</span>
      </div>
      
      <div class="data-item">
        <span class="label">ALTITUDE</span>
        <span class="value">{{ altitude.toFixed(0) }} <span class="unit">FT</span></span>
      </div>
      
      <div class="data-item">
        <span class="label">SPEED</span>
        <span class="value">{{ speed.toFixed(0) }} <span class="unit">KTS</span></span>
      </div>
      
      <div class="data-item">
        <span class="label">HEADING</span>
        <span class="value">{{ heading.toFixed(0) }}°</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.flight-data-card {
  position: absolute;
  top: 20px;
  right: 20px;
  width: 280px;
  background: rgba(17, 24, 39, 0.75); /* Dark gray/blue with opacity */
  backdrop-filter: blur(12px);
  -webkit-backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  padding: 16px;
  color: #fff;
  font-family: 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
  z-index: 1000; /* Ensure it's above the map */
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);
}

.header {
  display: flex;
  align-items: center;
  margin-bottom: 16px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  padding-bottom: 12px;
}

.status-indicator {
  width: 8px;
  height: 8px;
  background-color: #10B981; /* Green */
  border-radius: 50%;
  margin-right: 10px;
  box-shadow: 0 0 8px #10B981;
}

h3 {
  margin: 0;
  font-size: 1.1rem;
  font-weight: 600;
  letter-spacing: 0.5px;
}

.data-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.data-item {
  display: flex;
  flex-direction: column;
}

.data-item:first-child {
  grid-column: span 2; /* Coordinates take full width */
}

.label {
  font-size: 0.7rem;
  color: #9CA3AF; /* Light gray */
  letter-spacing: 1px;
  margin-bottom: 4px;
  font-weight: 500;
}

.value {
  font-size: 1rem;
  font-weight: 500;
  font-family: 'Consolas', 'Monaco', monospace; /* Monospaced for numbers */
}

.unit {
  font-size: 0.75rem;
  color: #6B7280;
}
</style>
