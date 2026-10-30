<#
.SYNOPSIS
    Draait de tests van Cities lokaal: de snelle tests zonder Unity (zoals CI) en de EditMode- en PlayMode-tests in Unity.

.DESCRIPTION
    Suites:
      Fast      EditMode-tests zonder Unity-runtime met dotnet, precies zoals de CI-workflow (ongeveer een minuut).
      EditMode  Alle EditMode-tests in Unity (batchmode).
      PlayMode  Alle PlayMode-tests in Unity (batchmode).
      Unity     EditMode en PlayMode.
      All       Fast, EditMode en PlayMode (standaard).

    De Unity-suites starten de editor in batchmode en kunnen dus niet draaien terwijl de editor het project open heeft;
    gebruik dan het Test Runner-venster in de editor, of draai het script op een tweede werkmap (git worktree) met
    -ProjectPath.

    Resultaten (NUnit-XML, logbestanden en dekking) komen in TestResults/ in de root van de repository.
    Exitcode 0 als alles slaagt, 1 bij mislukte tests, 2 bij een fout in de opzet (geen editor, project open, ...).

.EXAMPLE
    ./tools/test.ps1
    ./tools/test.ps1 -Suite Fast
    ./tools/test.ps1 -Suite EditMode -Filter "Cities.Tests.EditMode.Persistence"
    ./tools/test.ps1 -Suite Unity -ProjectPath ../CitiesGame-worktree/Cities
#>
[CmdletBinding()]
param(
    [ValidateSet('Fast', 'EditMode', 'PlayMode', 'Unity', 'All')]
    [string] $Suite = 'All',

    # Deel van een testnaam of namespace; voor Unity -testFilter (regex), voor dotnet FullyQualifiedName~.
    [string] $Filter,

    # Map van het Unity-project (met Assets/ en ProjectSettings/).
    [string] $ProjectPath,

    # Pad naar de Unity-editor; standaard $env:UNITY_EDITOR of de Hub-installatie van de projectversie.
    [string] $UnityPath,

    # Maakt bij Fast ook een dekkingsrapport (opencover) zoals CI.
    [switch] $Coverage
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $ProjectPath) {
    $ProjectPath = Join-Path $repoRoot 'Cities'
}

$ProjectPath = (Resolve-Path $ProjectPath).Path
# De repository van het project (bij een worktree een andere map dan die van dit script); Fast test die code.
$projectRepo = Split-Path -Parent $ProjectPath
$resultsDir = Join-Path $repoRoot 'TestResults'
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

class SetupError : System.Exception {
    SetupError([string] $message) : base($message) {}
}

function Get-ProjectEditorVersion {
    $versionFile = Join-Path $ProjectPath 'ProjectSettings/ProjectVersion.txt'
    if (-not (Test-Path $versionFile)) {
        throw [SetupError]::new("Geen Unity-project in $ProjectPath (ProjectSettings/ProjectVersion.txt ontbreekt).")
    }

    $line = Get-Content $versionFile | Where-Object { $_ -match '^m_EditorVersion:\s*(\S+)' } | Select-Object -First 1
    return ($line -replace '^m_EditorVersion:\s*', '').Trim()
}

function Resolve-UnityEditor {
    if ($UnityPath) {
        if (-not (Test-Path $UnityPath)) {
            throw [SetupError]::new("Unity-editor niet gevonden: $UnityPath")
        }

        return (Resolve-Path $UnityPath).Path
    }

    $version = Get-ProjectEditorVersion
    $candidates = @()
    if ($env:UNITY_EDITOR) { $candidates += $env:UNITY_EDITOR }
    if ($env:ProgramFiles) { $candidates += (Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe") }
    $candidates += "/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity"
    if ($env:HOME) { $candidates += (Join-Path $env:HOME "Unity/Hub/Editor/$version/Editor/Unity") }

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path $candidate)) {
            return (Resolve-Path $candidate).Path
        }
    }

    throw [SetupError]::new("Unity $version niet gevonden. Installeer hem via de Unity Hub of geef -UnityPath (of `$env:UNITY_EDITOR) op.")
}

function Assert-ProjectNotOpen {
    # De editor houdt Temp/UnityLockfile exclusief open zolang hij het project geopend heeft.
    $lockFile = Join-Path $ProjectPath 'Temp/UnityLockfile'
    if (-not (Test-Path $lockFile)) {
        return
    }

    try {
        $stream = [System.IO.File]::Open($lockFile, 'Open', 'ReadWrite', 'None')
        $stream.Dispose()
    }
    catch [System.IO.IOException] {
        throw [SetupError]::new("De Unity-editor heeft $ProjectPath open. Sluit de editor, gebruik het Test Runner-venster, of draai op een git worktree met -ProjectPath.")
    }
}

function Show-NUnitResults([string] $resultsFile, [string] $name) {
    if (-not (Test-Path $resultsFile)) {
        Write-Host "  $name`: geen resultaten ($resultsFile ontbreekt)" -ForegroundColor Red
        return $false
    }

    [xml] $xml = Get-Content -Raw -Encoding UTF8 $resultsFile
    $run = $xml.'test-run'
    $summary = "  {0}: {1} geslaagd, {2} mislukt, {3} overgeslagen van {4} ({5:N1} s)" -f `
        $name, $run.passed, $run.failed, $run.skipped, $run.total, [double]::Parse($run.duration, [cultureinfo]::InvariantCulture)
    $ok = ([int] $run.failed -eq 0) -and ([int] $run.total -gt 0)
    Write-Host $summary -ForegroundColor ($(if ($ok) { 'Green' } else { 'Red' }))

    foreach ($case in $xml.SelectNodes("//test-case[@result='Failed']")) {
        $message = $case.SelectSingleNode('failure/message')
        $text = if ($message) { $message.InnerText.Trim() } else { '' }
        Write-Host "    FAIL $($case.fullname)" -ForegroundColor Red
        if ($text) {
            ($text -split "`n" | Select-Object -First 6) | ForEach-Object { Write-Host "         $_" }
        }
    }

    return $ok
}

function Invoke-UnitySuite([string] $platform) {
    $editor = Resolve-UnityEditor
    Assert-ProjectNotOpen

    $resultsFile = Join-Path $resultsDir "$platform.xml"
    $logFile = Join-Path $resultsDir "$platform.log"
    Remove-Item -Force -ErrorAction SilentlyContinue $resultsFile
    $arguments = @('-batchmode', '-projectPath', "`"$ProjectPath`"", '-runTests', '-testPlatform', $platform,
        '-testResults', "`"$resultsFile`"", '-logFile', "`"$logFile`"")
    if ($platform -eq 'EditMode') {
        $arguments += '-nographics'
    }

    if ($Filter) {
        $arguments += @('-testFilter', "`"$Filter`"")
    }

    Write-Host "Unity $platform-tests (log: $logFile) ..."
    $process = Start-Process -FilePath $editor -ArgumentList $arguments -PassThru -Wait -NoNewWindow
    $ok = Show-NUnitResults $resultsFile $platform
    # Exitcode 2 betekent mislukte tests (die staan hierboven); al het andere behalve 0 is een fout van Unity zelf.
    if (-not $ok -and $process.ExitCode -ne 2) {
        Write-Host "  Unity stopte met code $($process.ExitCode); zie $logFile" -ForegroundColor Red
        Get-Content $logFile -Tail 15 -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "    $_" }
    }

    return $ok
}

function Get-PackagePath([string] $package) {
    # Een verse worktree heeft nog geen Library; dan de packages van de repository van dit script gebruiken.
    $caches = @((Join-Path $ProjectPath 'Library/PackageCache'), (Join-Path $repoRoot 'Cities/Library/PackageCache'))
    $cache = $caches | ForEach-Object { Get-ChildItem -Directory -ErrorAction SilentlyContinue $_ } |
        Where-Object { $_.Name -like "$package@*" } | Select-Object -First 1
    if (-not $cache) {
        throw [SetupError]::new("$package ontbreekt in Library/PackageCache; open het project één keer in Unity, zodat de packages er staan.")
    }

    return $cache.FullName
}

function Invoke-FastSuite {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw [SetupError]::new('dotnet niet gevonden; installeer de .NET 9 SDK.')
    }

    $editor = Resolve-UnityEditor
    $editorRoot = Split-Path -Parent $editor
    $managed = @((Join-Path $editorRoot 'Data/Managed'), (Join-Path $editorRoot '../Managed')) |
        Where-Object { Test-Path (Join-Path $_ 'UnityEngine') } | Select-Object -First 1
    if (-not $managed) {
        throw [SetupError]::new("Data/Managed van de editor niet gevonden naast $editor.")
    }

    $project = Join-Path $projectRepo 'ci/sonar/Cities.Sonar.Tests/Cities.Sonar.Tests.csproj'
    if (-not (Test-Path $project)) {
        throw [SetupError]::new("Testproject niet gevonden: $project")
    }

    $resultsFile = Join-Path $resultsDir 'Fast.trx'
    $testFilter = 'FullyQualifiedName!~Cities.Tests.PlayMode&TestCategory!=UnityRuntime'
    if ($Filter) {
        $testFilter += "&FullyQualifiedName~$Filter"
    }

    $arguments = @('test', $project, "-p:UnityManaged=$managed", "-p:BurstPackage=$(Get-PackagePath 'com.unity.burst')",
        "-p:InputSystemPackage=$(Get-PackagePath 'com.unity.inputsystem')", '--filter', $testFilter,
        '--logger', "trx;LogFileName=$resultsFile", '--results-directory', (Join-Path $resultsDir 'Fast'), '-nologo')
    if ($Coverage) {
        $arguments += @('--collect', 'XPlat Code Coverage;Format=opencover')
    }

    Write-Host 'Snelle tests zonder Unity (dotnet, zoals CI) ...'
    Remove-Item -Force -ErrorAction SilentlyContinue $resultsFile
    & dotnet @arguments | Where-Object { $_ -match ': error |warning CS' } | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    $exitCode = $LASTEXITCODE
    $ok = (Show-TrxResults $resultsFile 'Fast') -and $exitCode -eq 0
    if (-not $ok -and $exitCode -ne 0 -and -not (Test-Path $resultsFile)) {
        Write-Host "  dotnet stopte met code $exitCode (bouwfout?)" -ForegroundColor Red
    }

    return $ok
}

function Show-TrxResults([string] $resultsFile, [string] $name) {
    if (-not (Test-Path $resultsFile)) {
        Write-Host "  $name`: geen resultaten" -ForegroundColor Red
        return $false
    }

    [xml] $xml = Get-Content -Raw -Encoding UTF8 $resultsFile
    $counters = $xml.TestRun.ResultSummary.Counters
    $skipped = [int] $counters.total - [int] $counters.executed
    $ok = ([int] $counters.failed -eq 0) -and ([int] $counters.passed -gt 0)
    $summary = "  {0}: {1} geslaagd, {2} mislukt, {3} overgeslagen van {4}" -f $name, $counters.passed, $counters.failed, $skipped, $counters.total
    Write-Host $summary -ForegroundColor ($(if ($ok) { 'Green' } else { 'Red' }))
    if ([int] $counters.total -eq 0) {
        Write-Host '    Geen tests gevonden; klopt het filter?' -ForegroundColor Red
    }

    # Via XPath, omdat een run zonder tests geen Results-element heeft (StrictMode).
    foreach ($result in $xml.SelectNodes("//*[local-name()='UnitTestResult'][@outcome='Failed']")) {
        Write-Host "    FAIL $($result.GetAttribute('testName'))" -ForegroundColor Red
        $message = $result.SelectSingleNode(".//*[local-name()='Message']")
        if ($message) {
            ($message.InnerText.Trim() -split "`n" | Select-Object -First 6) | ForEach-Object { Write-Host "         $_" }
        }
    }

    return $ok
}

$suites = switch ($Suite) {
    'Fast' { @('Fast') }
    'EditMode' { @('EditMode') }
    'PlayMode' { @('PlayMode') }
    'Unity' { @('EditMode', 'PlayMode') }
    default { @('Fast', 'EditMode', 'PlayMode') }
}

$failed = @()
try {
    foreach ($name in $suites) {
        $ok = if ($name -eq 'Fast') { Invoke-FastSuite } else { Invoke-UnitySuite $name }
        if (-not $ok) {
            $failed += $name
        }
    }
}
catch [SetupError] {
    Write-Host "Fout: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}

if ($failed.Count -gt 0) {
    Write-Host "Mislukt: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}

Write-Host 'Alle tests geslaagd.' -ForegroundColor Green
exit 0
