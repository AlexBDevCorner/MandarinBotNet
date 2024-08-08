FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS build

WORKDIR /app

COPY *.sln ./
COPY MandarinBotNet/MandarinBotNet.csproj MandarinBotNet/
COPY DiscordBot/DiscordBot.csproj DiscordBot/
RUN dotnet restore

COPY . .

RUN dotnet build --no-restore -c Release

RUN dotnet publish MandarinBotNet/MandarinBotNet.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

COPY --from=build /out .

EXPOSE 80

ENTRYPOINT ["dotnet", "MandarinBotNet.dll"]