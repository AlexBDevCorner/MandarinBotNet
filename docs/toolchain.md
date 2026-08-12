# .NET toolchain baseline

MandarinBotNet targets .NET 10 LTS. Use the stable .NET SDK pinned in
[`global.json`](../global.json); `rollForward` is deliberately disabled so local,
CI, and container builds use the same SDK feature band and patch.

The production image uses the corresponding pinned .NET runtime. Do not replace
either image or SDK tag with a floating `10.0` tag: update the SDK, runtime, and
`global.json` together after validating the solution.

## Migration assessment

The application has no applicable .NET 9 Worker Service or timezone breaking
changes. In .NET 10, the runtime no longer installs its own default `SIGTERM`
handler; this application uses the Generic Host and `RunAsync`, whose
`ConsoleLifetime` continues to handle container `SIGTERM` gracefully. The
shutdown regression test verifies that hosted-service cleanup runs during that
path. No preview SDK or API is used.

Before opening a change, run the same validation performed by CI:

```powershell
dotnet restore MandarinBotNet.sln --locked-mode
dotnet build MandarinBotNet.sln --configuration Release --no-restore
dotnet test MandarinBotNet.sln --configuration Release --no-build --no-restore
dotnet format MandarinBotNet.sln --verify-no-changes --no-restore
```

The shutdown test covers the Generic Host's graceful `SIGTERM` path used by
`docker stop`; the schedule tests cover Europe/Riga DST behavior and Quartz
misfire handling. Validate the deployment image separately with `docker build`.
