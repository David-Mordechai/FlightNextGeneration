# FlightNextGeneration - Project Status

## Project Overview
Next-generation flight control and visualization system with C4I entity management.

## Features Implemented
- **C4I Entities (No-Fly Zones)**
  - Backend service `C4IEntities` (.NET 10 Web API) with PostGIS database.
  - Geospatial persistence for polygons and rectangles.
  - Frontend management interface using Leaflet and Leaflet-Draw.
  - Features: Create, Load, Edit (Geometry & Metadata), and Delete No-Fly Zones.
  - Auto-centering map on UAV detection.
- **Flight Visualization** (Pre-existing)
  - Real-time UAV tracking via SignalR.
  - Historical and projected path visualization.

## Technical Details
- **Backend:** .NET 10, Entity Framework Core, Npgsql (PostGIS), NetTopologySuite.
- **Frontend:** Vue 3 (Composition API), Vite, Leaflet, SignalR.
- **Infrastructure:** Docker Compose (PostGIS).

## Key Files
- `Backend/C4IEntities/`: C4I Management Service.
- `frontend/src/composables/useC4ILayer.ts`: C4I logic refactored into composables.
- `frontend/src/composables/useFlightLayer.ts`: Flight logic refactored into composables.
- `frontend/src/components/MapComponent.vue`: Main map orchestrator.
- `frontend/src/leaflet-patch.ts`: Global fixes for Leaflet/Leaflet-Draw compatibility.
