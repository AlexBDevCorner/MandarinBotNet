# Base images are pinned to their multi-platform manifest digests. Update the
# version and digest together after validating the new image in CI.
FROM mcr.microsoft.com/dotnet/runtime:10.0.10@sha256:68d35011fe04a39cca38208d392ed48f2df15653633dca16dbc4582d07342b9f AS base
WORKDIR /app
RUN install --directory --owner=app --group=app /app/data

FROM mcr.microsoft.com/dotnet/sdk:10.0.302@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["NuGet.Config", "."]
COPY ["MandarinBotNet/MandarinBotNet.csproj", "MandarinBotNet/"]
COPY ["MandarinBotNet/packages.lock.json", "MandarinBotNet/"]
COPY ["DiscordBot.Common/DiscordBot.Common.csproj", "DiscordBot.Common/"]
COPY ["DiscordBot.Common/packages.lock.json", "DiscordBot.Common/"]
COPY ["DiscordBot/DiscordBot.csproj", "DiscordBot/"]
COPY ["DiscordBot/packages.lock.json", "DiscordBot/"]
COPY ["DiscordBot.EventWatch/DiscordBot.EventWatch.csproj", "DiscordBot.EventWatch/"]
COPY ["DiscordBot.EventWatch/packages.lock.json", "DiscordBot.EventWatch/"]
COPY ["DiscordBot.FantasyPremierLeague/DiscordBot.FantasyPremierLeague.csproj", "DiscordBot.FantasyPremierLeague/"]
COPY ["DiscordBot.FantasyPremierLeague/packages.lock.json", "DiscordBot.FantasyPremierLeague/"]
COPY ["DiscordBot.UclFantasy/DiscordBot.UclFantasy.csproj", "DiscordBot.UclFantasy/"]
COPY ["DiscordBot.UclFantasy/packages.lock.json", "DiscordBot.UclFantasy/"]
RUN dotnet restore "./MandarinBotNet/MandarinBotNet.csproj" --locked-mode
COPY . .
WORKDIR "/src/MandarinBotNet"

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MandarinBotNet.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish --chown=app:app /app/publish .
USER app
HEALTHCHECK --interval=5s --timeout=5s --start-period=10s --retries=2 \
  CMD ["dotnet", "MandarinBotNet.dll", "--health-check", "/app/data/health-state.json", "15"]
ENTRYPOINT ["dotnet", "MandarinBotNet.dll"]
