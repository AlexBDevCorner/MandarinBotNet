# Dependency policy

All NuGet package versions are declared in `Directory.Packages.props` through
Central Package Management. Projects contain only `PackageReference` entries,
so a dependency's version is selected once for the entire solution.

## Runtime packages

| Package family | Selected version | Rationale |
| --- | --- | --- |
| .NET libraries (`Microsoft.Data.Sqlite`, `Microsoft.Extensions.*`) | 10.0.11 (10.9.0 for `Microsoft.Extensions.Http.Resilience`) | Aligned with the .NET 10 target framework. |
| Discord.Net | 3.20.1 | Current stable version; compatible with the application's .NET 10 target. |
| Quartz and Quartz.Extensions.Hosting | 3.19.1 | Current 3.x stable line, preserving the existing `IJob.Execute` Task-based contract. |
| TimeZoneConverter | 7.2.0 | Current stable release compatible with modern .NET. |
| SQLitePCLRaw.bundle_e_sqlite3 | 3.0.5 | Current stable SQLite native bundle. |

The solution uses lock files. Run `dotnet restore --force-evaluate` when
intentionally changing a central package version, then commit the updated
`packages.lock.json` files. Before submitting an upgrade, run `dotnet test` and
`dotnet list package --vulnerable` against the configured NuGet sources.
