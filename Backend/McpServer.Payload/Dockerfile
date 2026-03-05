FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["McpServer.Payload/McpServer.Payload.csproj", "McpServer.Payload/"]
RUN dotnet restore "./McpServer.Payload/McpServer.Payload.csproj"
COPY . .
WORKDIR "/src/McpServer.Payload"
RUN dotnet build "./McpServer.Payload.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./McpServer.Payload.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "McpServer.Payload.dll"]
