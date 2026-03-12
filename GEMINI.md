# FlightNextGeneration - Project Status

## Agent Development Mandates
- **Plan-then-Approve Workflow:** Before performing any code modifications (Directives), the agent MUST first research the issue, formulate a technical strategy, and present a detailed plan to the user. Implementation may ONLY proceed after explicit user approval of the proposed plan.
- **Inquiry vs. Directive:** All requests are treated as Inquiries (research/analysis) by default. The agent shall not initiate changes based on observations or bugs until a Directive is issued and the corresponding plan is approved.

## Project Overview
Next-generation flight control and visualization system with C4I entity management and AI-driven mission planning.

## Optimized Unified Agent Architecture
The project has migrated from a distributed microservice architecture to a high-performance **Unified Agent System** (`Backend/AiAgents` project). This architecture is designed for speed, resource efficiency, and massive tool scaling (1000+ tools):

- **Single Process / Shared Inference:** All specialized agents (Router, Flight, Payload, Mission) run within a single ASP.NET Core process. They share a single `IChatCompletionService` instance, keeping the local LLM (Ollama) "warm" and eliminating redundant model loading.
- **Tier 1 - In-Process Routing:** The **Router Agent** analyzes user intent and identifies required domains. This handoff happens in-memory with zero network latency.
- **Tier 2 - Isolated Kernels (Scalability):** To support **1000+ tools**, each specialized agent receives its own **Isolated Semantic Kernel** instance containing only its domain-specific tools. This prevents LLM context overload and ensures high accuracy.
- **Performance:** Unified architecture reduced complex command latency from **~88 seconds** (distributed) to **~16 seconds** (unified).

## Features Implemented
- **Voice Control & Co-pilot (Operation "Voice Command")**
  - **Local Speech Engine:** Offline TTS (SherpaOnnx) and STT (Whisper.net).
  - **Interaction Flow:** Hold-to-Speak -> Tactial Greeting -> Tactical Chirp -> Recording.
  - **Robustness:** Resampling to 16kHz and AudioContext auto-unlock for browser compliance.

- **Visual Overhaul (Operation "Satellite Command")**
  - **Map:** Esri World Imagery realistic satellite view.
  - **3D Markers:** High-visibility ground-clamped Tactical Beacons and Antenna icons.
  - **Materials:** Custom `TacticalBeamMaterialProperty` for "Digital Pulse Beam" projected paths.
  - **Labels:** Glassmorphism tactical tags with dynamic DTG (Distance to Go) and ETA.

- **Advanced Flight Simulation**
  - **Precision Navigation:** Reduced waypoint arrival threshold to 11m.
  - **Orbit Entry:** UAV enters 3km orbit perimeter immediately upon arrival.
  - **Dynamic Physics:** Smooth interpolation of speed and altitude.

- **Real-time Tactical Tracing**
  - **Console Logs:** Agents broadcast granular "Thoughts" and "Tool Executions" directly to the browser console via SignalR.
  - **Trace Sequence:** `Routing` -> `Agent Reasoning` -> `Tool Call (Args)` -> `Tool Result` -> `Final Summary`.

## Technical Details
- **Backend:** .NET 10, Unified Agentic System, Semantic Kernel (Isolated Kernels), Entity Framework Core, Npgsql (PostGIS).
- **Inference:** Ollama (Local Llama 3.2), persistent GPU VRAM caching (`OLLAMA_KEEP_ALIVE=24h`).
- **Frontend:** Vue 3, CesiumJS, Tailwind CSS, SignalR.

## Key Files
- `Backend/AiAgents/Program.cs`: Master orchestration and in-process agent delegation.
- `Backend/AiAgents/Router/RouterAgent.cs`: Intent classification and domain routing.
- `Backend/AiAgents/FlightControl/FlightTools.cs`: Navigation and flight dynamics logic.
- `Backend/AiAgents/Payload/PayloadTools.cs`: Camera gimbal and sensor locking logic.
- `Backend/Bff.Service/Services/AiChatService.cs`: Gateway to the unified agent system.
- `frontend/src/services/SignalRService.ts`: Tactical AI trace logging to browser console.
