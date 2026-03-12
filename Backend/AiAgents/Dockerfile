FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AiAgents/AiAgents.csproj", "AiAgents/"]
RUN dotnet restore "AiAgents/AiAgents.csproj"
COPY . .
WORKDIR "/src/AiAgents"
RUN dotnet build "AiAgents.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AiAgents.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AiAgents.dll"]
