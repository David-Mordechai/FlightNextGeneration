# FlightNextGeneration - Project Status

## Agent Development Mandates
- **Plan-then-Approve Workflow:** Before performing any code modifications (Directives), the agent MUST first research the issue, formulate a technical strategy, and present a detailed plan to the user. Implementation may ONLY proceed after explicit user approval of the proposed plan.
- **Inquiry vs. Directive:** All requests are treated as Inquiries (research/analysis) by default. The agent shall not initiate changes based on observations or bugs until a Directive is issued and the corresponding plan is approved.

## Project Overview
Next-generation flight control and visualization system with C4I entity management and AI-driven mission planning.

## Scalable Agent Architecture
The project utilizes a high-performance, multi-tiered agent architecture (`Backend/Agents.*` projects) optimized for precise intent delegation and technical execution:
- **Tier 1 - Orchestration & Routing:** A **Router Agent** (GLM-4 30B) analyzes user intent and delegates tasks to specialized agents. It utilizes a semantic classification system to determine required domains (Flight, Mission, Payload).
- **Tier 2 - Specialized Technical Agents:** Domain-specific kernels (all powered by GLM-4 30B for maximum reliability) execute complex logic using **Native Semantic Kernel Tools**:
  - **Mission Control Agent:** Manages persistent operational points and No-Fly Zones.
  - **Flight Control Agent:** Handles UAV navigation (Optimal pathfinding), speed, and altitude telemetry.
  - **Payload Agent:** Controls camera gimbal locking and sensor resets.
- **Truth Guard & Response Hardening:** A verification layer ensures 100% truthful reporting. If tools succeed, agents return a strict, professional technical summary (e.g., 'Executed: [command]') while ignoring LLM chat fluff. If tools fail, the failure is explicitly reported.

## Features Implemented
- **Voice Control & Co-pilot (Operation "Voice Command")**
  - **Local Speech Engine:** Replaced Google TTS with **SherpaOnnx** (Offline TTS) and **Whisper.net** (Offline STT).
  - **Co-pilot Experience:**
    - **Wake Word:** First-click interaction performs a backend AI readiness check.
    - **Greeting:** "I am here to assist" (Audio + Text).
    - **Interaction Flow:** Hold-to-Speak -> "How can I help?" -> **Tactical Chirp** -> Record.
    - **Feedback:** "Processing [text]..." transient message appears instantly and is replaced by the real response.
    - **Visuals:** Mic button pulses red and input placeholder changes to "RECORDING..." instantly.
  - **Robustness:**
    - **Audio Unlock:** Auto-resumes AudioContext on first user interaction to comply with browser autoplay policies.
    - **Resampling:** Implemented cross-platform linear interpolation to ensure 16kHz audio compliance for Whisper.
    - **State Machine:** Robust "Idle -> Checking -> Ready -> Recording" logic with cancellation support.

- **Visual Overhaul (Operation "Satellite Command")**
  - **Map:** Switched to **Esri World Imagery** for high-resolution realistic satellite view.
  - **Markers (Cesium 3D Upgrade):** 
    - **Home:** High-visibility **Antenna Icon** (`antena.png`) with glassmorphism label.
    - **Target:** Tactical **Target Beacon** (`target.Png`) with ground-clamped billboard.
  - **Flight Path:**
    - **Optimal Path (Green):** Semi-transparent emerald line representing the calculated safe route.
    - **Projected Path (Cyan):** **"Digital Pulse Beam"**, a custom shader effect (`TacticalBeamMaterialProperty`) indicating current intent.
    - **UAV Trail:** Integrated into the digital pulse beam logic for directionality.
  - **No-Fly Zones:**
    - **Style:** Semi-transparent red volumes with thin outlines, providing clear hazard awareness without obscuring terrain.
  - **Labels:**
    - **Style:** "Glassmorphism" Tactical Tags (Semi-transparent slate, blur effect, cyan accents).
    - **Dynamic Data:** UAV labels feature stacked **DTG** (Distance to Go) and **ETA** vertically.
    - **Scaling:** Adaptive pixel offsets and scaling based on distance to prevent clutter.

- **Advanced Flight Simulation**
  - **Precision Navigation:** Reduced waypoint arrival threshold to **11m** for precise cornering.
  - **Orbit Entry:** UAV enters orbit immediately upon reaching the 3km perimeter, avoiding center-point "jumps".
  - **Dynamic Physics:** Smooth interpolation of speed and altitude during mission changes.

- **AI Mission Planning**
  - **Automated Sensor:** Flight system automatically locks the camera on the target during transit if no manual lock is set.
  - **Native Tooling:** Tier 2 agents use native `[KernelFunction]` tools for high-speed execution, replacing redundant MCP infrastructure.
  - **Hardened Responses:** AI provides concise, technical confirmations (e.g. 'Executed: [command]'). Markdown and conversational filler are stripped for TTS clarity.
  - **Strict Literalism:** Mandate enforced to use exact location names (e.g., 'Target A' vs 'Alpha') to prevent resolution failures.
  - **Data Freshness:** AI fetches fresh entity lists from DB for every operation.


- **User Interface Enhancements**
  - **Picture-in-Picture (PiP):** 
    - Isolated Cesium viewer context (`pip.html`) for the sensor feed to prevent WebGL context conflicts.
    - **HUD:** Tactical overlay with REC status, coordinates, altitude, pitch/yaw, and zoom controls.
    - **Sensor Accuracy:** 
        - **HPR Basis Matrix:** 100% orientation parity between UAV model and camera view.
        - **Range Clamping:** Hard **20km Limit** on sensor footprint and entity rendering.
        - **Visual Parity:** Tuned Video Fog (`0.00025`) matching the 20km projection limit.
  - **Mission Chat:** 
    - **Performance Metrics:** Integrated "AI Response Timing" (e.g., "1.45s") next to messages.
    - **Optimistic UI:** Instant "Processing..." feedback and user message rendering.

- **Observability & AI Tracing**
  - **Distributed Tracing:** Full request lifecycle visibility via **OpenTelemetry**.
  - **AI Thought Process:** Integrated `Microsoft.Extensions.AI` tracing to capture tool invocations and LLM results.
  - **Centralized Dashboard:** .NET Aspire Dashboard for monitoring traces, metrics, and logs.

## Technical Details
- **Backend:** .NET 10, Multi-Tiered Agent Architecture (Semantic Routing), Native Semantic Kernel Tools, Entity Framework Core, Npgsql (PostGIS).
- **Speech:** SherpaOnnx (TTS), Whisper.net (STT).
- **Frontend:** Vue 3, CesiumJS, Tailwind CSS, SignalR.

## Key Files
- `Backend/Agents.Router/Program.cs`: Core orchestration and intent delegation logic.
- `Backend/Agents.FlightControl/FlightTools.cs`: Native flight mechanics and pathfinding logic.
- `Backend/Agents.Payload/PayloadTools.cs`: Native gimbal and sensor control logic.
- `Backend/Bff.Service/Services/AiChatService.cs`: Intelligent response merging and agent dispatch.
- `frontend/src/composables/useCesiumFlightLayer.ts`: 3D flight visualization & sensor footprint.