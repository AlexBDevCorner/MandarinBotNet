[CmdletBinding()]
param(
    [string]$Path = (Join-Path $PSScriptRoot "..\.env.tools")
)

$resolvedPath = Resolve-Path -LiteralPath $Path -ErrorAction Stop
$loadedNames = [System.Collections.Generic.List[string]]::new()

foreach ($line in Get-Content -LiteralPath $resolvedPath) {
    $trimmed = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
        continue
    }

    $parts = $trimmed -split "=", 2
    if ($parts.Count -ne 2) {
        throw "Invalid environment entry in $resolvedPath. Expected NAME=VALUE."
    }

    $name = $parts[0].Trim()
    $value = $parts[1].Trim()
    if ($value.Length -ge 2 -and
        (($value.StartsWith('"') -and $value.EndsWith('"')) -or
         ($value.StartsWith("'") -and $value.EndsWith("'")))) {
        $value = $value.Substring(1, $value.Length - 2)
    }

    if ($name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') {
        throw "Invalid environment variable name '$name' in $resolvedPath."
    }

    if (-not [string]::IsNullOrWhiteSpace($value)) {
        [Environment]::SetEnvironmentVariable($name, $value, 'Process')
        $loadedNames.Add($name)
    }
}

if ($loadedNames.Count -eq 0) {
    Write-Host "No non-empty tool environment variables were loaded."
} else {
    Write-Host "Loaded tool environment variables into this PowerShell process: $($loadedNames -join ', ')"
}
