# Agent tooling setup

This repository keeps Codex skills and MCP configuration local to the project:

- `.agents/skills/aspnet-core`
- `.agents/skills/security-best-practices`
- `.codex/config.toml`

The MCP configuration contains only server addresses and environment-variable
names. Never put PATs, bot tokens, OCI keys, or session tokens in that file.

## First activation

1. Trust this repository in Codex so project `.codex/config.toml` is loaded.
2. Restart Codex (or restart the IDE extension) after the initial setup.
3. Open the MCP panel or run `/mcp` and confirm that `context7`, `github`,
   `github_actions`, and `oracle_oci` are listed. Servers without credentials
   may be listed but will not complete authenticated calls yet.

## Local environment file

Create the ignored local file and edit it without committing it:

```powershell
Copy-Item .env.tools.example .env.tools
notepad .env.tools
```

To load its non-empty values into the current PowerShell process, allow local
scripts for this process only and run the importer:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\Import-AgentToolEnv.ps1
```

Then start Codex CLI or an IDE from that same process so it inherits the
variables. As a one-command alternative, the following launcher imports the
file and starts Codex CLI without changing the persistent execution policy:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\Start-CodexWithTools.ps1
```

This launcher does not start the Codex desktop app. An already-running desktop
process cannot inherit variables added later. For GitHub in the desktop app,
either start the app from a process containing `GITHUB_PAT_TOKEN`, persist that
variable at Windows user scope, or prefer the GitHub plugin's OAuth flow. OCI
does not require a desktop environment variable in this repository because its
non-secret profile name is pinned in `.codex/config.toml`.

## Context7 authentication

Context7 works without a key at lower limits. To raise the limits, obtain a key
from Context7 and set `CONTEXT7_API_KEY` in `.env.tools`, reload the file, and
restart the Codex client that will use it.

## GitHub authentication

Create a fine-grained PAT in GitHub and restrict repository access to
`AlexBDevCorner/MandarinBotNet`.

Start with read-only permissions:

- Metadata: read (GitHub includes this automatically)
- Contents: read
- Actions: read
- Pull requests and Issues: read only if those MCP operations are needed

Add write permissions only for operations you intend agents to perform. For
example, Actions write is needed to trigger workflows, while Contents or Pull
requests write is needed to change repository content or create/update PRs.

Put the token only in the ignored `.env.tools` file:

```dotenv
GITHUB_PAT_TOKEN=github_pat_replace_me
```

Then import the file and restart/launch Codex from that environment. Both
GitHub MCP entries use the same variable. Their write-capable tools are set to
request approval.

If a PAT is exposed or committed, revoke it immediately, create a replacement,
and remove the leaked value from Git history as a separate incident-response
step.

## OCI authentication

### How it works in the Windows Codex app

The Codex app launches Oracle directly from an ignored, repository-local
Python 3.13 virtual environment. This avoids both the unusable Microsoft Store
Python alias and non-JSON Windows PowerShell startup text under Codex's Windows
sandbox. Recreate the runtime from a normal PowerShell session with:

```powershell
uv python install 3.13 `
  --install-dir .tools\uv-python `
  --no-bin --no-registry `
  --cache-dir .tools\uv-cache

$python = Get-ChildItem -Recurse -Filter python.exe .tools\uv-python |
  Select-Object -First 1 -ExpandProperty FullName
uv venv .tools\oracle-mcp --python $python --cache-dir .tools\uv-cache
uv pip install --python .tools\oracle-mcp\Scripts\python.exe `
  --cache-dir .tools\uv-cache `
  oracle.oci-cloud-mcp-server==2.1.0
```

Codex runs that environment through `scripts/oracle_oci_mcp_windows.py`.
Oracle OCI MCP 2.1.0 hardcodes the Unix
audit path `/tmp/audit.log`, which fails on Windows when `C:\tmp` is absent.
The Python launcher redirects only that audit handler to the ignored
`.tools\oracle-oci-mcp\audit.log` path and then calls Oracle's unchanged server
entry point. It also disables FastMCP's server banner so stdout contains only
MCP JSON messages. The launcher does not read or copy OCI credentials.

The server reads the named `mandarinbot-agent` profile from:

```text
%USERPROFILE%\.oci\config
```

The profile name is stored in `.codex/config.toml`; it is not secret. OCI keys
and session tokens stay under `%USERPROFILE%\.oci`, outside the repository.
The Codex app and the MCP server do not read `.env.tools` for OCI.

The repository-local OCI CLI executable is installed at:

```text
.tools\oci-cli\Scripts\oci.exe
```

The directory is intentionally ignored. Recreate it on another checkout with:

```powershell
uv venv .tools\oci-cli
uv pip install --python .tools\oci-cli\Scripts\python.exe oci-cli
```

For initial interactive use, create a short-lived browser-authenticated
security-token profile. Replace `<your-home-region>` with the OCI region shown
as your tenancy's home region in the Oracle Console:

```powershell
& .\.tools\oci-cli\Scripts\oci.exe session authenticate `
  --region <your-home-region> `
  --profile-name mandarinbot-agent
```

The command opens an Oracle login page in your browser and writes the standard
OCI configuration under `%USERPROFILE%\.oci`, outside this repository.

Validate the profile before using the MCP server:

```powershell
& .\.tools\oci-cli\Scripts\oci.exe iam region list `
  --profile mandarinbot-agent `
  --auth security_token
```

Security-token sessions are short-lived. When the session expires, run
`session authenticate` again. Oracle's OCI MCP server detects
`security_token_file` in the selected profile and uses it. Restart the
`oracle_oci` MCP server or the Codex app if an already-running server continues
to return an authentication error.

For more convenient long-lived use, `oci setup config` creates an API-key
profile:

```powershell
& .\.tools\oci-cli\Scripts\oci.exe setup config
```

On a machine with no existing OCI config, that wizard creates the `DEFAULT`
profile. Either change `OCI_CONFIG_PROFILE` in `.codex/config.toml` to
`DEFAULT`, or add a matching `mandarinbot-agent` profile before using the MCP
server. Prefer a dedicated, least-privilege OCI IAM user for this long-lived
agent profile.

Upload only the generated public key to OCI. Keep the private key and OCI config
under `%USERPROFILE%\.oci`; never copy them into this repository. Grant the OCI
principal access only to the compartment and resource families needed for this
VM migration. The OCI MCP `invoke_oci_api` tool can perform destructive calls,
so this repository prompts for every OCI MCP invocation.

### Start and verify in Codex app

1. Fully quit and reopen the Codex app after creating or refreshing the profile.
2. Reopen this trusted repository.
3. Open **Settings > MCP servers**, or type `/mcp`, and find `oracle_oci`.
4. Start with a read-only request such as `Use oracle_oci to list OCI regions`.
5. Approve the tool call after reviewing its client, operation, and parameters.

The generic `invoke_oci_api` tool can represent either a read or a write, which
is why every OCI MCP invocation is configured to prompt for approval.

## Verification prompts

After restarting with credentials available, try read-only requests first:

- `List recent GitHub Actions runs for this repository.`
- `List OCI compute instances in the deployment compartment.`
- `Fetch current ASP.NET Core container guidance with Context7.`

Do not provision, terminate, redeploy, rotate credentials, or change firewall
rules until the intended target and rollback path have been reviewed.
