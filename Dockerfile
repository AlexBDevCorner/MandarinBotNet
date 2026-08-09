FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app
RUN mkdir -p /app/data

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MandarinBotNet/MandarinBotNet.csproj", "MandarinBotNet/"]
COPY ["DiscordBot/DiscordBot.csproj", "DiscordBot/"]
RUN dotnet restore "./MandarinBotNet/MandarinBotNet.csproj"
COPY . .
WORKDIR "/src/MandarinBotNet"
RUN dotnet build "./MandarinBotNet.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MandarinBotNet.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
HEALTHCHECK --interval=5s --timeout=5s --start-period=10s --retries=2 \
  CMD ["dotnet", "MandarinBotNet.dll", "--health-check", "/app/data/health-state.json", "15"]
ENTRYPOINT ["dotnet", "MandarinBotNet.dll"]
