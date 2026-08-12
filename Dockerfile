FROM mcr.microsoft.com/dotnet/runtime:10.0.10 AS base
WORKDIR /app
RUN mkdir -p /app/data

FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["NuGet.Config", "."]
COPY ["MandarinBotNet/MandarinBotNet.csproj", "MandarinBotNet/"]
COPY ["MandarinBotNet/packages.lock.json", "MandarinBotNet/"]
COPY ["DiscordBot/DiscordBot.csproj", "DiscordBot/"]
COPY ["DiscordBot/packages.lock.json", "DiscordBot/"]
RUN dotnet restore "./MandarinBotNet/MandarinBotNet.csproj" --locked-mode
COPY . .
WORKDIR "/src/MandarinBotNet"
RUN dotnet build "./MandarinBotNet.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MandarinBotNet.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
HEALTHCHECK --interval=5s --timeout=5s --start-period=10s --retries=2 \
  CMD ["dotnet", "MandarinBotNet.dll", "--health-check", "/app/data/health-state.json", "15"]
ENTRYPOINT ["dotnet", "MandarinBotNet.dll"]
