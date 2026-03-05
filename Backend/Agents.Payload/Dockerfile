FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Agents.Payload/Agents.Payload.csproj", "Agents.Payload/"]
COPY ["Agents.Core/Agents.Core.csproj", "Agents.Core/"]
RUN dotnet restore "Agents.Payload/Agents.Payload.csproj"
COPY . .
WORKDIR "/src/Agents.Payload"
RUN dotnet build "Agents.Payload.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Agents.Payload.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Agents.Payload.dll"]
