# FlightNextGeneration - Project Status

## Project Overview
Next-generation flight control and visualization system with C4I entity management and AI-driven mission planning.

## Features Implemented
- **AI Mission Planning (New)**
  - **Optimal Pathfinding:** Calculates the shortest route avoiding No-Fly Zones using a Visibility Graph algorithm (Dijkstra).
  - **Interactive Workflow:**
    1.  User asks AI to find a path.
    2.  System calculates and visualizes the path on the map (Green dashed line).
    3.  User confirms/approves.
    4.  System executes the flight plan.
  - **Tools:** `CalculateOptimalPath`, `ExecuteFlightPlan`.

- **C4I Entities (No-Fly Zones)**
  - Backend service `C4IEntities` (.NET 10 Web API) with PostGIS database.
  - Geospatial persistence for polygons and rectangles.
  - Frontend management interface using Leaflet and Leaflet-Draw.
  - Features: Create, Load, Edit (Geometry & Metadata), and Delete No-Fly Zones.
  - Robust error handling and Leaflet compatibility fixes.
  - **Fix:** Added logic to enforce 2D geometries (removing Z coordinates) to prevent database errors.
  - **Visualization:** Added permanent labels with cyan text positioned on the right of entities.

- **Points (New)**
  - **Entities:** Home (Start) and Target (End) locations.
  - **Interaction:**
    - Place markers using the "Marker" draw tool.
    - Specify name and type (Home/Target) in a dedicated modal.
    - Custom SVG icons for visualization.
  - **Backend:** `Points` API (`PointsController`) with geospatial `Point` storage.
  - **Tools:** `PointsController`, `PointModal`.
  - **Status:** Verified. Migration `InitialCreate` reset and recreated.

- **MCP Tools**
  - **NavigateTo:** Updated to resolve locations against the `Points` database instead of global city names. User refers to points by name (e.g., "Home", "Target").

- **Flight Visualization**
  - Real-time UAV tracking via SignalR.
  - **Note:** Historical path visualization (blue tail) has been removed for a cleaner view.
  - Projected path visualization remains.
  - **Waypoint Navigation:** UAV now follows a queue of waypoints for complex paths.
  - **Custom UAV Icon:** Replaced default SVG with `Orbiter3.png` for enhanced visualization.

## Technical Details
- **Backend:** .NET 10, Entity Framework Core, Npgsql (PostGIS), NetTopologySuite.
- **Frontend:** Vue 3 (Composition API), Vite, Leaflet, SignalR.
- **AI/MCP:** Model Context Protocol integration for autonomous tool use.
- **Infrastructure:** Docker Compose (PostGIS).

## Key Files
- `Backend/C4IEntities/Services/PathFindingService.cs`: Visibility Graph algorithm.
- `Backend/McpServer.FlightControl/Tools.cs`: AI Tools for path calculation and execution.
- `frontend/src/composables/useC4ILayer.ts`: C4I logic refactored into composables.
- `frontend/src/composables/useFlightLayer.ts`: Flight logic refactored into composables.
- `frontend/src/components/MapComponent.vue`: Main map orchestrator.