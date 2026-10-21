<#
.SYNOPSIS
    Builds the Rime Giant (Elite Creatures Reborn): measures the game's Troll, builds its Blender parts to fit, then in
    Unity the kit and the bundle for Windows and Linux.
.EXAMPLE
    .\assets\ecr_rimegiant\build.ps1 -Preview -Install
    Everything, plus stills and a video in assets\ecr_rimegiant\out\preview, and the bundles copied into the mod.
.NOTES
    1. Measure (when out\rimegiant\fit.json is missing, or with -Measure): Unity's RimeFit poses the game's Troll and
       writes where each plate sits and the skin under it, and the snow line over the sleeping troll.
    2. Blender: ecr_rime_plate_0..7, ecr_rime_crust and ecr_rime_crust_1..6, ecr_rime_boulder (each an
       assets\<name>\model.py; the shared code is in this folder).
    3. Unity: RimeBuild builds the kit ecr_rimegiant_kit and the bundle ecr_rimegiant with the kit and ecr_rime_boulder.
    Unity code: unity\Assets\Editor\RimeGiant. The Troll comes from the reference export (rip-reference.ps1) and never
    goes into the bundle. Close the Unity editor first: batch mode cannot open a project the editor has open.
#>
param(
    [switch]$Preview,
    [switch]$Install,
    [switch]$SkipBlender,
    [switch]$Measure,
    [string]$Blender = $(if ($env:WORKSHOP_BLENDER) { $env:WORKSHOP_BLENDER } else { "$env:USERPROFILE\tools\blender\blender.exe" }),
    [string]$Unity = $(if ($env:WORKSHOP_UNITY) { $env:WORKSHOP_UNITY } else { "$env:USERPROFILE\tools\Unity\Editor\6000.0.75f1\Editor\Unity.exe" })
)
$ErrorActionPreference = 'Stop'
$workshop = Resolve-Path "$PSScriptRoot\..\.."
$parts = @(0..7 | ForEach-Object { "ecr_rime_plate_$_" }) + 'ecr_rime_crust' + @(1..6 | ForEach-Object { "ecr_rime_crust_$_" }) + 'ecr_rime_boulder'
$fit = Join-Path $workshop 'out\rimegiant\fit.json'
$out = Join-Path $workshop 'out\bundles\ecr_rimegiant'
$previewOut = Join-Path $PSScriptRoot 'out\preview'
$modBundles = Join-Path $workshop '..\EliteCreaturesPack\EliteCreaturesPack\assets\bundles'

function Invoke-Unity([string]$Method, [string[]]$Arguments, [string]$Log) {
    if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Unity is running; close it first (batch mode cannot share the project)' }
    $argv = @('-batchmode', '-projectPath', "$workshop\unity", '-executeMethod', $Method, '-logFile', $Log) + $Arguments
    $argv = $argv | ForEach-Object { if ($_ -match '\s') { "`"$_`"" } else { $_ } }
    Write-Host "Unity: $Method"
    $process = Start-Process -FilePath $Unity -ArgumentList $argv -Wait -PassThru -NoNewWindow
    Get-Content $Log | Where-Object { $_ -cmatch 'WORKSHOP|error CS' } | ForEach-Object { Write-Host "  $_" }
    if ($process.ExitCode -ne 0) { throw "Unity failed (exit $($process.ExitCode)); full log: $Log" }
}

if ($Measure -or -not (Test-Path $fit)) {
    Invoke-Unity 'Workshop.RimeGiant.RimeFit.Run' @('-workshopFit', $fit) (Join-Path $workshop 'out\rime_fit.log')
}
if (-not $SkipBlender) { & "$workshop\build.ps1" -Asset $parts }

$stage = Join-Path $workshop 'unity\Assets\Bundles\ecr_rimegiant'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
foreach ($part in $parts) {
    $dest = New-Item -ItemType Directory -Force (Join-Path $stage $part)
    Copy-Item "$workshop\assets\$part\out\$part.fbx", "$workshop\assets\$part\out\$part.json", "$workshop\assets\$part\out\${part}_*.png" -Destination $dest
}

if (Test-Path $previewOut) { Remove-Item $previewOut -Recurse -Force }
$arguments = @('-workshopFit', $fit, '-workshopOut', $out)
if ($Preview) { $arguments += @('-workshopPreview', $previewOut) }
Invoke-Unity 'Workshop.RimeGiant.RimeBuild.Run' $arguments (Join-Path $workshop 'out\rimegiant_unity.log')

if ($Preview) {
    & $Blender --background --factory-startup --python "$workshop\blender\encode.py" -- "$previewOut\frames" "$previewOut\rimegiant_idle_walk_punch.mp4" 30 2>&1 |
        Where-Object { $_ -match 'Error|Traceback' } | ForEach-Object { Write-Host "  $_" }
    Write-Host "Preview: $previewOut"
}
if ($Install) {
    Copy-Item "$out\ecr_rimegiant.windows", "$out\ecr_rimegiant.linux" -Destination $modBundles
    Write-Host "Installed the bundles into $modBundles"
}
