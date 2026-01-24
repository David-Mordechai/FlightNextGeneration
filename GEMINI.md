# FlightNextGeneration - Project Status

## Project Overview
Next-generation flight control and visualization system with C4I entity management and AI-driven mission planning.

## Features Implemented
- **Voice Control & Co-pilot (Operation "Voice Command")**
  - **Local Speech Engine:** Replaced Google TTS with **SherpaOnnx** (Offline TTS) and **Whisper.net** (Offline STT).
  - **Co-pilot Experience:**
    - **Wake Word:** First-click interaction performs a backend AI readiness check.
    - **Greeting:** "I am here to assist" (Audio + Text).
    - **Interaction Flow:** Hold-to-Speak -> "How can I help?" -> **Tactical Chirp** -> Record.
    - **Feedback:** "Processing..." transient message appears instantly and is replaced by the real response.
    - **Visuals:** Mic button pulses red and input placeholder changes to "RECORDING..." instantly.
  - **Robustness:**
    - **Audio Unlock:** Auto-resumes AudioContext on first user interaction to comply with browser autoplay policies.
    - **Resampling:** Implemented cross-platform linear interpolation to ensure 16kHz audio compliance for Whisper.
    - **State Machine:** Robust "Idle -> Preamble -> Initializing -> Recording" logic with cancellation support.

- **Visual Overhaul (Operation "Satellite Command")**
  - **Map:** Switched to **Esri World Imagery** for high-resolution realistic satellite view.
  - **Markers (Cesium 3D Upgrade):** 
    - **Home:** Cyan **"Scanner Base"** featuring a semi-transparent cylinder with a rotating holographic ring.
    - **Target:** Red **"Pulsating Beacon"** with dynamic height animation and dashed tactical rings.
  - **Flight Path:**
    - **Optimal Path:** Implemented **"Digital Flow" Material**, a custom shader effect with flowing green data gradients.
    - **Projected Path:** Tactical Dashed Line (Blue) for intent visualization.
    - **UAV Trail:** Added a cyan glowing ribbon trail that follows the aircraft.
  - **No-Fly Zones:**
    - **Style:** **"Force Field"** effect using grid materials with a glowing neon rim at max altitude.
  - **Labels:**
    - **Style:** "Glassmorphism" Tactical Tags (Semi-transparent dark slate, blur effect, cyan accent).
    - **Behavior:** Dynamic scaling (shrink on zoom out) and Z-ordering (always below UAV/Icons) to prevent clutter.

- **Advanced Flight Simulation**
  - **Precision Navigation:** Reduced waypoint arrival threshold from 111m to **11m** to prevent "corner cutting".
  - **Drift Prevention:** Implemented "Snap-to-Waypoint" logic to ensure every flight leg starts from the precise geometric origin.
  - **Tangent Orbit Entry:** UAV now enters orbit **immediately upon reaching the perimeter** (1km out) instead of flying to the center and "jumping", creating a realistic spiral entry visual.
  - **Safety Buffer:** Increased No-Fly Zone avoidance buffer to **55m** to guarantee clearance even with minor tracking errors.

- **AI Mission Planning**
  - **Simplified Workflow:** AI uses a single "NavigateTo" tool that handles route calculation and sensor locking automatically.
  - **Automated Sensor:** `NavigateTo` tool automatically commands `PointPayload` to lock the camera on the destination.
  - **Strict Response Style:** AI instructed to respond with a single, concise sentence (No markdown).
  - **Optimal Pathfinding:** Calculates shortest routes avoiding No-Fly Zones.
  - **Natural Language Control:** Navigate to named points via AI.
  - **Strict Data Freshness:** AI instructed to always fetch fresh entity lists from DB, never relying on conversation history cache.

- **C4I Entities & Points**
  - **Points:** Home (Start) and Target (End) locations.
  - **No-Fly Zones:** Geospatial persistence for polygons and rectangles.
  - **Real-Time Sync:** Instant map updates via SignalR when AI modifies entities.

- **Infrastructure**
  - **Docker Compose:** Exposed ports for `flightcontrol` and `c4ientities` services.
  - **Observability Stack:** Implemented a central observability system using **.NET Aspire Dashboard**.
    - **Tracing & Metrics:** Integrated **OpenTelemetry** across all backend services.
    - **Logs:** Centralized logging via **Serilog** with the OpenTelemetry sink.
  - **Frontend Build Fix:** Resolved TypeScript compilation errors (`unused variables`) that were preventing the production build and rendering of the application.

- **User Interface Enhancements**
  - **Picture-in-Picture (PiP):** 
    - Implemented `pip.html` and `PipApp.vue` for a dedicated, isolated Cesium viewer context.
    - Integrated `SensorFeed.vue` using an `iframe` to provide WebGL context isolation for the sensor feed.
    - Resolved browser warnings by correctly configuring `iframe` sandbox attributes.
    - **Sensor Accuracy:** 
        - Implemented **HPR Basis Matrix** method for Sensor Footprint to ensure 100% orientation parity with the UAV model.
        - **Telemetry Synchronization:** Updated the projection logic to use the **exact latest telemetry coordinates** (`data.lat/lng/alt`) instead of the interpolated UAV entity position.
        - **Range Clamping (Verified):** Implemented a hard **20km Limit** on the sensor footprint. Verified via E2E test (`tests/sensor-projection.spec.ts`) that the map projection never exceeds this distance.
        - **Visual Parity:** 
            - Tuned Video Fog Density (`0.00025`) to match the 20km map limit.
            - **Hard Entity Culling:** Implemented `DistanceDisplayCondition` on simulated entities in the Video Feed. Targets are now strictly culled (not rendered) if distance > 20km, guaranteeing 100% consistency with the map projection limit even if fog is insufficient.
        - **Elevation-Aware Targeting:** Updated Backend Flight Simulation to account for **Target Altitude** when calculating gimbal pitch. This fixes the vertical misalignment where targets on terrain appeared "above" the crosshair because the UAV was aiming at sea level.
  - **UAV Labels:** Stacked Distance and ETA vertically with 'DST:' and 'ETA:' prefixes for better readability.
  - **Mission Chat:** 
    - Implemented "AI Response Timing" display.
    - Increased font sizes and improved text visibility for better readability.
    - Scaled "Mission Control" title to match chat content.
  - **UAV Visualization:** 
    - Increased UAV icon size to **80px** for tactical clarity.
    - **Projected Path (Blue):** Enhanced with a thicker (3px) solid neon line and a high-energy pulse animation to indicate direction/intent without dashing.

- **Advanced Observability & AI Tracing**
  - **AI Thought Process:** Integrated `Microsoft.Extensions.AI.OpenTelemetry` to capture detailed traces of AI completions, tool invocations (MCP), and results.
  - **Distributed Tracing:** Full request lifecycle visibility from User Chat -> AI Logic -> MCP Tool Call -> Database (EF Core) -> Final Response.

## Technical Details
- **Backend:** .NET 10, Entity Framework Core, Npgsql (PostGIS), NetTopologySuite.
- **Speech:** SherpaOnnx (TTS), Whisper.net (STT), AudioWorklet (Frontend Capture).
- **Frontend:** Vue 3, Vite, Tailwind CSS, DaisyUI, Leaflet, SignalR.
- **Visuals:** Custom CSS animations, SVG-in-DivIcon markers, dynamic Z-indexing.

## Key Files
- `Backend/Bff.Service/Services/SpeechService.cs`: Offline TTS/STT logic with resampling.
- `frontend/src/composables/useVoiceComms.ts`: AudioWorklet recording, beep sequencing, and state management.
- `frontend/src/components/MissionChat.vue`: Chat UI with optimistic updates and readiness checks.
- `Backend/McpServer.FlightControl/Tools.cs`: Navigation logic with auto-sensor lock.