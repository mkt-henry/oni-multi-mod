# Builds, stamps, deploys, and repackages the mod for the test PC.
#
# The stamp is the point. Two machines running different builds behave like a
# desync rather than like a version problem, so every build gets an identifier
# that shows up in the game log and in the installer output.
#
#   pwsh -File tools\deploy.ps1

$ErrorActionPreference = 'Stop'

$repo    = Split-Path -Parent $PSScriptRoot
$modName = 'ONI_Together_dev'
$deploy  = Join-Path $env:USERPROFILE "Documents\Klei\OxygenNotIncluded\mods\dev\$modName"
$stage   = 'C:\dev\oni-multi-testpc'

# The game holds the dll open; copying over it half-succeeds and the failure is confusing later.
if (Get-Process -Name 'OxygenNotIncluded' -ErrorAction SilentlyContinue) {
    throw 'Oxygen Not Included is running. Close it before deploying.'
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
# The publicizer tool targets net6.0 with roll-forward pinned off, and .NET 6 is not installed.
$env:DOTNET_ROLL_FORWARD = 'Major'
# MinVer shells out to git, which is not on PowerShell's PATH here.
$env:PATH = "C:\Program Files\Git\cmd;$env:PATH"

Push-Location $repo
try {
    $branch = (& git rev-parse --abbrev-ref HEAD).Trim()
    $sha    = (& git rev-parse --short HEAD).Trim()
    $dirty  = if ((& git status --porcelain)) { '+dirty' } else { '' }
    $when   = (Get-Date).ToString('yyyy-MM-dd HH:mm')
    $stamp  = "$when  $branch@$sha$dirty"

    Write-Host "building  $stamp"

    # A clean tree needs two passes: Shared resolves its references before the publicizer
    # has produced them, so the first build fails on missing game types.
    & dotnet build ONI_Together.sln -c Release --nologo -v quiet | Out-Null
    if ($LASTEXITCODE -ne 0) {
        & dotnet build ONI_Together.sln -c Release --nologo -v quiet | Out-Null
    }
    if ($LASTEXITCODE -ne 0) { throw "build failed ($LASTEXITCODE)" }

    Set-Content -Path (Join-Path $deploy 'BUILD.txt') -Value $stamp -Encoding utf8 -NoNewline

    if (-not (Test-Path $stage)) { New-Item -ItemType Directory -Force $stage | Out-Null }
    robocopy $deploy (Join-Path $stage $modName) /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    Copy-Item (Join-Path $PSScriptRoot 'install-mod.bat') $stage -Force

    $hash = (Get-FileHash (Join-Path $deploy 'ONI_Together.dll') -Algorithm SHA256).Hash

    Write-Host ''
    Write-Host '---------------------------------------------------------------'
    Write-Host "  build   $stamp"
    Write-Host "  sha256  $hash"
    Write-Host "  deploy  $deploy"
    Write-Host "  package $stage"
    Write-Host '---------------------------------------------------------------'
}
finally {
    Pop-Location
}
