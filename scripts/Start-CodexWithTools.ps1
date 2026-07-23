[CmdletBinding()]
param(
    [string]$EnvironmentFile = (Join-Path $PSScriptRoot "..\.env.tools"),
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$CodexArguments
)

& (Join-Path $PSScriptRoot 'Import-AgentToolEnv.ps1') -Path $EnvironmentFile
if (-not $?) {
    exit 1
}

& codex @CodexArguments
exit $LASTEXITCODE
