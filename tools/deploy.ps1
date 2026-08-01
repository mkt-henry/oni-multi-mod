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

    # Publish to the download branch as part of deploying, not as a step to remember. The two
    # machines have to run the same build, and a stale second PC presents as a broken feature.
    # git writes progress to stderr, which PowerShell turns into terminating errors under
    # ErrorActionPreference=Stop. Relax it for this block rather than redirecting, since redirecting
    # a native command's stderr is what wraps those lines in ErrorRecords in the first place.
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    $wt = Join-Path $env:TEMP 'oni-dist-wt'
    if (Test-Path $wt) { & git worktree remove --force $wt | Out-Null }
    & git worktree prune | Out-Null
    & git worktree add --detach $wt | Out-Null
    Push-Location $wt
    try {
        & git checkout -B dist-testpc | Out-Null
        Get-ChildItem $wt -Force | Where-Object { $_.Name -ne '.git' } | Remove-Item -Recurse -Force
        Copy-Item (Join-Path $stage '*') $wt -Recurse -Force
        Set-Content (Join-Path $wt 'README.md') @"
# Test PC package

Built mod for the second test machine. Binaries live here on purpose so a
machine with no build tooling can install from a zip. Replaced wholesale on
every deploy - whatever is here is the current build.

    $stamp

## Install

1. Download and extract this branch
2. Close Oxygen Not Included
3. Run ``install-mod.bat``
4. Check the printed build line matches the host
"@ -Encoding utf8
        & git add -f . | Out-Null
        & git -c user.name='mkt-henry' -c user.email='bpark0718@gmail.com' commit -q -m "dist: $stamp" | Out-Null
        & git push -f -q origin dist-testpc | Out-Null
    }
    finally {
        Pop-Location
        & git worktree remove --force $wt | Out-Null
        $ErrorActionPreference = $previousPreference
    }

    Write-Host ''
    Write-Host '---------------------------------------------------------------'
    Write-Host "  build   $stamp"
    Write-Host "  sha256  $hash"
    Write-Host "  deploy  $deploy"
    Write-Host "  package $stage"
    Write-Host '  testpc  https://github.com/mkt-henry/oni-multi-mod/archive/refs/heads/dist-testpc.zip'
    Write-Host '---------------------------------------------------------------'
}
finally {
    Pop-Location
}
