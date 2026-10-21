<#
.SYNOPSIS
    Builds the Kraken (Elite Creatures Pack): the tentacle and head in Blender, the eye and ink textures, then in Unity
    the prefabs, the contract check and the bundle ecp_kraken for Windows and Linux.
.EXAMPLE
    .\assets\ecp_kraken\build.ps1 -Preview -Install
    Everything, plus stills and kraken_sheet.png in assets\ecp_kraken\out\preview, and the bundles copied into the mod.
.NOTES
    1. textures.py (plain Python, numpy + Pillow): ecp_kraken_eye_albedo and ecp_kraken_ink_0..3.
    2. Blender (kraken.py --part tentacle, --part head): model, bake, export as JSON (mesh, weights, rig, markers in
       Unity's axes) with the baked PNGs and quick stills in out\tentacle and out\head.
    3. Unity (Workshop.Kraken.KrakenBuild): prefabs ecp_kraken_tentacle and ecp_kraken_head, KrakenCheck (fails the
       build when the contract breaks), out\bundles\ecp_kraken\report.txt, the bundles; with -Preview the stills, then
       sheet.py makes kraken_sheet.png. The longship in the stills comes from the reference export (rip-reference.ps1)
       and never goes into the bundle.
    Unity code: unity\Assets\Editor\Kraken. If another Unity batch build holds the project, this waits for it.
#>
param(
    [switch]$Preview,
    [switch]$Install,
    [switch]$SkipBlender,
    [string]$Blender = $(if ($env:WORKSHOP_BLENDER) { $env:WORKSHOP_BLENDER } else { "$env:USERPROFILE\tools\blender\blender.exe" }),
    [string]$Unity = $(if ($env:WORKSHOP_UNITY) { $env:WORKSHOP_UNITY } else { "$env:USERPROFILE\tools\Unity\Editor\6000.0.75f1\Editor\Unity.exe" })
)
$ErrorActionPreference = 'Stop'
$workshop = Resolve-Path "$PSScriptRoot\..\.."
$here = $PSScriptRoot
$out = Join-Path $workshop 'out\bundles\ecp_kraken'
$previewOut = Join-Path $here 'out\preview'
$stage = Join-Path $workshop 'unity\Assets\Bundles\ecp_kraken'
$modBundles = Join-Path $workshop '..\EliteCreaturesPack\EliteCreaturesPack\assets\bundles'

function Invoke-Blender([string]$Part) {
    $ErrorActionPreference = 'Continue'   # Blender writes warnings to stderr
    Write-Host "Blender: $Part"
    $output = & $Blender --background --factory-startup --python-exit-code 1 --python "$here\kraken.py" -- --part $Part 2>&1 | ForEach-Object { "$_" }
    $failed = $LASTEXITCODE -ne 0
    $output | Where-Object { $_ -match '^WORKSHOP|Error|Traceback' } | ForEach-Object { Write-Host "  $_" }
    if ($failed) {
        $output | Select-Object -Last 25 | ForEach-Object { Write-Host "  $_" }
        throw "Blender failed for $Part"
    }
}

function Wait-Unity {
    for ($i = 0; (Get-Process Unity -ErrorAction SilentlyContinue); $i++) {
        if ($i -ge 80) { throw 'Unity has been running for 20 minutes; close it first (batch mode cannot share the project)' }
        if ($i -eq 0) { Write-Host 'Unity is running (another build?); waiting for it to finish' }
        Start-Sleep -Seconds 15
    }
}

function Invoke-Unity([string]$Method, [string[]]$Arguments, [string]$Log) {
    Wait-Unity
    $argv = @('-batchmode', '-projectPath', "$workshop\unity", '-executeMethod', $Method, '-logFile', $Log) + $Arguments
    $argv = $argv | ForEach-Object { if ($_ -match '\s') { "`"$_`"" } else { $_ } }
    Write-Host "Unity: $Method"
    $process = Start-Process -FilePath $Unity -ArgumentList $argv -Wait -PassThru -NoNewWindow
    Get-Content $Log | Where-Object { $_ -cmatch 'WORKSHOP|error CS' } | ForEach-Object { Write-Host "  $_" }
    if ($process.ExitCode -ne 0) { throw "Unity failed (exit $($process.ExitCode)); full log: $Log" }
}

& python "$here\textures.py" "$here\out\textures"
if ($LASTEXITCODE -ne 0) { throw 'textures.py failed' }
if (-not $SkipBlender) {
    Invoke-Blender 'tentacle'
    Invoke-Blender 'head'
}

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item "$here\out\tentacle\ecp_kraken_tentacle.json", "$here\out\tentacle\ecp_kraken_tentacle_*.png" -Destination $stage
Copy-Item "$here\out\head\ecp_kraken_head.json", "$here\out\head\ecp_kraken_skin_*.png" -Destination $stage
Copy-Item "$here\out\textures\*.png" -Destination $stage

New-Item -ItemType Directory -Force $out | Out-Null
if (Test-Path $previewOut) { Remove-Item $previewOut -Recurse -Force }
$arguments = @('-workshopOut', $out)
if ($Preview) { $arguments += @('-workshopPreview', $previewOut) }
Invoke-Unity 'Workshop.Kraken.KrakenBuild.Run' $arguments (Join-Path $workshop 'out\kraken_unity.log')
Write-Host "Report: $out\report.txt"

if ($Preview) {
    & python "$here\sheet.py" $previewOut
    Write-Host "Preview: $previewOut"
}
if ($Install) {
    Copy-Item "$out\ecp_kraken.windows", "$out\ecp_kraken.linux" -Destination $modBundles
    Write-Host "Installed the bundles into $modBundles"
}
