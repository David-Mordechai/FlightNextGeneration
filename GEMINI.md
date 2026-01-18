# FlightNextGeneration - Project Status

## Project Overview
Next-generation flight control and visualization system with C4I entity management and AI-driven mission planning.

## Features Implemented
- **Visual Overhaul (Operation "Satellite Command")**
  - **Map:** Switched to **Esri World Imagery** for high-resolution realistic satellite view.
  - **Markers:** Replaced static icons with **Animated HTML/CSS Markers**:
    - **Home:** Blue "Pulse Radar" ring with centered base icon.
    - **Target:** Red "Crosshair" with rotating brackets and glowing center.
  - **Flight Path:**
    - **Safe Corridor (Green):** Thinner, neon-green path with a **"Digital Flow" animation** (marching dashes) to indicate direction.
    - **Projected Path:** Refined to a sharp, pulsing blue neon line.
    - **Logic:** Fixed visual detachment bugs by syncing updates with zoom events.
  - **Labels:**
    - **Style:** "Glassmorphism" Tactical Tags (Semi-transparent dark slate, blur effect, cyan accent).
    - **Behavior:** Dynamic scaling (shrink on zoom out) and Z-ordering (always below UAV/Icons) to prevent clutter.

- **Advanced Flight Simulation**
  - **Precision Navigation:** Reduced waypoint arrival threshold from 111m to **11m** to prevent "corner cutting".
  - **Drift Prevention:** Implemented "Snap-to-Waypoint" logic to ensure every flight leg starts from the precise geometric origin.
  - **Tangent Orbit Entry:** UAV now enters orbit **immediately upon reaching the perimeter** (1km out) instead of flying to the center and "jumping", creating a realistic spiral entry visual.
  - **Safety Buffer:** Increased No-Fly Zone avoidance buffer to **55m** to guarantee clearance even with minor tracking errors.

- **AI Mission Planning**
  - **Optimal Pathfinding:** Calculates shortest routes avoiding No-Fly Zones.
  - **Natural Language Control:** Navigate to named points via AI.
  - **Strict Data Freshness:** AI instructed to always fetch fresh entity lists from DB, never relying on conversation history cache.

- **C4I Entities & Points**
  - **Points:** Home (Start) and Target (End) locations.
  - **No-Fly Zones:** Geospatial persistence for polygons and rectangles.
  - **Real-Time Sync:** Instant map updates via SignalR when AI modifies entities.

## Technical Details
- **Backend:** .NET 10, Entity Framework Core, Npgsql (PostGIS), NetTopologySuite.
- **Frontend:** Vue 3, Vite, Tailwind CSS, DaisyUI, Leaflet, SignalR.
- **Visuals:** Custom CSS animations, SVG-in-DivIcon markers, dynamic Z-indexing.

## Key Files
- `frontend/src/style.css`: Core visual effects (Neon, Glassmorphism, Animations).
- `frontend/src/composables/useFlightLayer.ts`: Flight path visualization and arrival logic.
- `Backend/Bff.Service/Services/FlightStateService.cs`: Physics engine (Orbit entry, Waypoint snapping).
- `Backend/C4IEntities/Services/PathFindingService.cs`: Navigation graph and safety buffers.