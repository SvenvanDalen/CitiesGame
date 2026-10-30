<#
.SYNOPSIS
    Zet de git-hooks van de repository aan (tools/hooks), zodat elke push eerst de snelle tests draait.
.DESCRIPTION
    Stelt core.hooksPath van deze repository in op tools/hooks. Ongedaan maken: git config --unset core.hooksPath.
#>
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
git -C $repoRoot config core.hooksPath tools/hooks
if ($LASTEXITCODE -ne 0) {
    throw 'git config mislukt.'
}

Write-Host "Hooks aan: $(git -C $repoRoot config core.hooksPath). Elke push draait nu tools/test.ps1 -Suite Fast."
