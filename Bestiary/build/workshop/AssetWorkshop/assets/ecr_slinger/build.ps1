<#
.SYNOPSIS
    Builds the Greydwarf Slinger (Elite Creatures Reborn): its four Blender parts, then in Unity the shot (a humanoid
    clip authored on the game's Greydwarf skeleton), the kit and the bundle for Windows and Linux.
.EXAMPLE
    .\assets\ecr_slinger\build.ps1 -Preview -Install
    Everything, plus stills and a video in assets\ecr_slinger\out\preview, and the bundles copied into the mod.
.NOTES
    Parts: ecr_slingshot, ecr_sling_band, ecr_sling_pouch, ecr_slinger_satchel (each an assets\<name>\model.py).
    Unity code: unity\Assets\Editor\Slinger. The Greydwarf comes from the reference export (rip-reference.ps1) and
    never goes into the bundle. Close the Unity editor first: batch mode cannot open a project the editor has open.
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
$parts = 'ecr_slingshot', 'ecr_sling_band', 'ecr_sling_pouch', 'ecr_slinger_satchel'
$out = Join-Path $workshop 'out\bundles\ecr_slinger'
$previewOut = Join-Path $PSScriptRoot 'out\preview'
$modBundles = Join-Path $workshop '..\EliteCreaturesPack\EliteCreaturesPack\assets\bundles'

if (-not $SkipBlender) { & "$workshop\build.ps1" -Asset $parts }

$stage = Join-Path $workshop 'unity\Assets\Bundles\ecr_slinger'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
foreach ($part in $parts) {
    $dest = New-Item -ItemType Directory -Force (Join-Path $stage $part)
    Copy-Item "$workshop\assets\$part\out\$part.fbx", "$workshop\assets\$part\out\$part.json", "$workshop\assets\$part\out\${part}_*.png" -Destination $dest
}

if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Unity is running; close it first (batch mode cannot share the project)' }
if (Test-Path $previewOut) { Remove-Item $previewOut -Recurse -Force }
$log = Join-Path $workshop 'out\slinger_unity.log'
$argv = @('-batchmode', '-projectPath', "$workshop\unity", '-executeMethod', 'Workshop.Slinger.SlingerBuild.Run',
          '-workshopOut', $out, '-logFile', $log)
if ($Preview) { $argv += @('-workshopPreview', $previewOut) }
$argv = $argv | ForEach-Object { if ($_ -match '\s') { "`"$_`"" } else { $_ } }
Write-Host 'Unity: slinger'
$process = Start-Process -FilePath $Unity -ArgumentList $argv -Wait -PassThru -NoNewWindow
Get-Content $log | Where-Object { $_ -match 'WORKSHOP|error CS' } | ForEach-Object { Write-Host "  $_" }
if ($process.ExitCode -ne 0) { throw "Unity failed (exit $($process.ExitCode)); full log: $log" }

if ($Preview) {
    foreach ($video in @(@('frames', 'slinger_shot.mp4'), @('frames_flight', 'slinger_flight.mp4'))) {
        & $Blender --background --factory-startup --python "$workshop\blender\encode.py" -- "$previewOut\$($video[0])" "$previewOut\$($video[1])" 30 2>&1 |
            Where-Object { $_ -match 'Error|Traceback' } | ForEach-Object { Write-Host "  $_" }
    }
    Write-Host "Preview: $previewOut"
}
if ($Install) {
    Copy-Item "$out\ecr_slinger.windows", "$out\ecr_slinger.linux" -Destination $modBundles
    Write-Host "Installed the bundles into $modBundles"
}
