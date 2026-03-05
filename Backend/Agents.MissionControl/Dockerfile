FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Agents.MissionControl/Agents.MissionControl.csproj", "Agents.MissionControl/"]
COPY ["Agents.Core/Agents.Core.csproj", "Agents.Core/"]
RUN dotnet restore "Agents.MissionControl/Agents.MissionControl.csproj"
COPY . .
WORKDIR "/src/Agents.MissionControl"
RUN dotnet build "Agents.MissionControl.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Agents.MissionControl.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Agents.MissionControl.dll"]
