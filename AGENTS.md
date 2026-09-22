# AGENTS.md — MandarinBotNet

Instructions for coding agents, including the OpenCode autonomous worker and
reviewers. The dispatched AutonomousWork task specification defines *what* to
build; this file defines the repository-level engineering constraints for *how*
to build it.

## Project overview

MandarinBotNet is a .NET 10 worker application hosting a Discord bot and
scheduled background jobs. It integrates Discord functionality, Fantasy Premier
League features, UEFA fantasy features, SQLite-backed persistence, health
checks, and Quartz scheduling.

The application composition root is `MandarinBotNet/Program.cs`. Dependency
registration is split into extension methods under `MandarinBotNet/Extensions/`.

## Solution layout

```text
MandarinBotNet.sln
MandarinBotNet/                         Worker host / composition root / Quartz wiring
DiscordBot.Common/                     Shared options, scheduling and common infrastructure
DiscordBot/                            Core Discord bot behavior and shared bot services
DiscordBot.EventWatch/                 Event/ticket monitoring integrations and jobs
DiscordBot.FantasyPremierLeague/       FPL integrations and scheduled jobs
DiscordBot.UclFantasy/                 UEFA fantasy integration and jobs
DiscordBot.Tests/                      NUnit test suite
docs/                                  Operational and architecture documentation
infra/terraform/                       OCI infrastructure
scripts/                               Deployment/support scripts
Dockerfile                             Production container image
```

Keep responsibilities in their existing projects. Do not move unrelated
functionality into the worker host merely because it is convenient.

## Build / test / verification

The repository pins the .NET SDK through `global.json`. Use the pinned SDK.

Normal verification:

```powershell
dotnet restore MandarinBotNet.sln --locked-mode
dotnet build MandarinBotNet.sln --configuration Release --no-restore
dotnet test MandarinBotNet.sln --configuration Release --no-build --no-restore
dotnet format MandarinBotNet.sln --verify-no-changes --no-restore
```

For changes affecting deployment scripts, workflows, containers or Terraform,
also run the relevant checks represented in `.github/workflows/ci.yml`.
When a change adds/removes a project, project reference, package dependency, or
other build-topology input used by the worker executable, also build the
production container locally:

```powershell
docker build --tag mandarinbot:verify --file Dockerfile .
```

A successful solution build is not sufficient proof that the production
container restore/publish path is still valid.

The autonomous task may explicitly require additional verification. Run it in
addition to these repository checks.

## Coding conventions

- Target .NET 10 and preserve the SDK/package pinning already present.
- Nullable reference types and implicit usings are enabled; match existing
  project style.
- Use Microsoft.Extensions dependency injection, configuration and logging.
- Use `ILogger<T>`; do not introduce ad-hoc console logging in application
  services.
- Use async APIs for I/O.
- Prefer focused services with explicit dependencies over service locators or
  static mutable state.
- Do not commit tokens, passwords, Discord credentials, GitHub credentials, or
  environment-specific secrets.
- Configuration secrets belong in runtime configuration/environment variables
  or the repository's existing secret-management mechanism, never appsettings.

## Scheduling rules

Quartz is the repository's scheduling mechanism.

- New scheduled work must integrate with the existing
  `AddMandarinBotScheduling()` registration.
- Prefer Quartz `IJob` implementations and normal Quartz triggers.
- Do not add a second timer framework, custom infinite delay loop, or unrelated
  hosted-service scheduler when Quartz can express the requirement.
- Scheduled jobs must log failures clearly and must not silently swallow errors.
- Prevent overlapping execution when overlap could cause duplicate external
  side effects.
- Keep scheduling/orchestration logic separate from Discord presentation logic.

## Configuration rules

`MandarinBotOptions` and its validator are the central strongly typed
configuration model.

- Add strongly typed options for new settings.
- Validate invalid operational configuration at startup.
- Safe defaults should disable externally visible behavior unless explicitly
  enabled.
- Never place secret default values in source control.
- Environment-variable names should follow the standard .NET double-underscore
  hierarchy.

## HTTP / external API rules

- Prefer `HttpClient` through dependency injection for external HTTP calls.
- Set required headers explicitly and validate non-success responses.
- Never log authorization headers, bearer tokens, or secret query parameters.
- Tests should use deterministic fake HTTP handlers/clients rather than real
  network calls where practical.

## Test conventions

- NUnit + AwesomeAssertions.
- Add or update tests for behavior changes.
- Keep tests deterministic: no dependency on live Discord, GitHub, FPL/UCL
  services, real wall-clock scheduling, or machine-specific paths.
- Exercise configuration validation and failure paths as well as successful
  behavior.
- Existing Quartz tests provide the preferred pattern for schedule-related
  changes.

## Deployment and infrastructure

Production runs as a container and has OCI/Terraform deployment support.

Do not modify `infra/`, deployment scripts, GitHub workflows, runtime
identities, or package versions unless the dispatched task explicitly requires
it. Infrastructure changes require focused verification and should not be
incidental to an application task.

The production `Dockerfile` manually enumerates project files and lock files
before its restore layer. If a task adds/removes a project or changes the
worker's project-reference graph, synchronizing those Dockerfile copy inputs is
required integration work for that task even when the task does not explicitly
name the Dockerfile. Keep such Dockerfile edits minimal and limited to preserving
the existing restore/publish topology.

## Autonomous worker rules

- You are given exactly ONE task specification through the read-only
  `control/` checkout. Implement only that task.
- Never modify anything under `control/`.
- Never invent follow-up work, priorities, dependencies, or requirements.
- Work on `autonomous/<TASK-ID>` from `main`.
- Push the branch immediately after creating it.
- If the branch or PR already exists for a retry/correction, reuse it. Never
  create a second task branch or PR.
- Preserve progress remotely: commit and push after meaningful milestones and
  make a checkpoint at least every 10–15 minutes during long milestones.
- For a new implementation, open one draft PR after the first pushed diff, then
  keep updating that same PR.
- A successful worker run must leave the PR ready for review, not draft.
- PR title must start with `[<TASK-ID>]`.
- Required labels: `autonomous`, `autonomous:opencode`,
  `task:<TASK-ID>`.
- Required PR sections: `## Task`, `## Control specification`,
  `## Implementation`, `## Verification`, `## Autonomous execution`.
- The verification section must contain real commands/results; never invent
  test evidence.
- Never merge the autonomous PR.
- Never force-push.
- Never inspect, reconstruct, print, decode or persist workflow credentials.
  Do not run `gh auth token`.
- Authentication failure is a worker failure, not permission to recover tokens
  from Git configuration or runner files.

## Must not modify during autonomous tasks

Unless the authoritative task explicitly requires otherwise:

- `control/` — authoritative read-only task checkout.
- `global.json`, central package versions, or target frameworks.
- `infra/` or deployment scripts.
- `Dockerfile`, except for the minimal project/lock-file synchronization
  required when the task itself changes the worker's project-reference graph.
- Anything explicitly listed under the task's `Out of scope`.

If the task itself explicitly requires one of the normally protected
application/configuration areas, follow the task. The worker must still never
modify `control/`; it remains the authoritative read-only task and authorization
boundary.
