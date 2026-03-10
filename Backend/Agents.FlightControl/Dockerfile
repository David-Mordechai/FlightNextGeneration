FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Agents.FlightControl/Agents.FlightControl.csproj", "Agents.FlightControl/"]
RUN dotnet restore "Agents.FlightControl/Agents.FlightControl.csproj"
COPY . .
WORKDIR "/src/Agents.FlightControl"
RUN dotnet build "Agents.FlightControl.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Agents.FlightControl.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Agents.FlightControl.dll"]
