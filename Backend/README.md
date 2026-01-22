# FlightNextGeneration Backend

This directory contains the backend services for the FlightNextGeneration project, orchestrated via Docker Compose.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running.
- (Optional) [Visual Studio 2022](https://visualstudio.microsoft.com/) for debugging.

## Getting Started

1.  **Navigate to the Backend directory:**
    ```bash
    cd Backend
    ```

2.  **Create configuration file:**
    Create a file named `.env` in this directory (`Backend/.env`). Copy the content from the [Configuration Reference](#configuration-reference) section below and adjust values if necessary (e.g., add your API keys).

3.  **Run the services:**
    ```bash
    docker compose up --build
    ```

    The services will be available at:
    - **BFF Service:** http://localhost:5135
    - **C4I Service:** http://localhost:5293
    - **MCP Server:** http://localhost:5116 (Internal)

## Configuration Reference (.env)

Copy the following content into a file named `.env` in the `Backend` folder:

```ini
# AI Provider Keys
# Leave empty if using Ollama
OPENAI_KEY=
GOOGLE_GEMINI_KEY=

# Ollama Configuration
# "host.docker.internal" allows the container to access Ollama running on your host machine.
OLLAMA_URL=http://host.docker.internal:11434
OLLAMA_MODEL=granite4:3b

# AI Provider Selection
# Options: Ollama, OpenAI, Gemini
AI_PROVIDER=Ollama
OPENAI_MODEL_NAME=gpt-4o
GOOGLE_GEMINI_MODEL=gemini-2.0-flash

# Postgres Configuration
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_DB=flightdb
```

## Project Structure

- **Bff.Service**: Backend for Frontend, handles SignalR and API aggregation.
- **C4IEntities**: Manages No-Fly Zones and Geospatial data (PostGIS).
- **McpServer.FlightControl**: AI Tool server (Model Context Protocol) for flight operations.
- **docker-compose.yml**: Defines the services and infrastructure configuration.
- **docker-compose.override.yml**: Defines development-specific settings (ports, volumes).
