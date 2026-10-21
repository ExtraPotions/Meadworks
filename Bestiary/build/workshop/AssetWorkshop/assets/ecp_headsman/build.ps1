<#
.SYNOPSIS
    Builds the headsman's animations (working name for Elite Creatures Pack's greataxe skeleton boss): the bone
    greataxe (assets\ecp_bone_greataxe) staged into Unity, the clips authored there on the game's Skeleton, the whole
    fight baked for Blender, its sounds (sfx.py), and the Blender scene that plays it.
.EXAMPLE
    .\assets\ecp_headsman\build.ps1 -Open
    Everything, then opens the sequence of all moves in Blender, playing.
.EXAMPLE
    .\assets\ecp_headsman\build.ps1 -Bundle -Install
    The mod's bundle (ecp_headsman.windows and .linux: clips, animator, the boss's axe in pieces, the players' greataxe,
    the axehead, their icons) built into out\bundles and copied into Elite Creatures Pack.
.EXAMPLE
    .\assets\ecp_headsman\build.ps1 -Player -Open
    The Executioner's Greataxe in a player's hands instead: every stance, a jump and the combo, in Blender.
.EXAMPLE
    .\assets\ecp_headsman\build.ps1 -Player -Combo -Open
    Only the greataxe's combo, from over the shoulder, the front and the side, with the game's Battleaxe sounds.
.EXAMPLE
    .\assets\ecp_headsman\build.ps1 -Probe
    Only logs the Skeleton's bones, the game clips' settings, the avatar's limits and the axe's grips.
.NOTES
    Unity code: unity\Assets\Editor\Headsman. The Skeleton comes from the reference export (rip-reference.ps1) and never
    goes into anything shipped. Close the Unity editor first: batch mode cannot open a project the editor has open.
#>
param(
    [switch]$Probe,
    [switch]$Stills,
    [switch]$Open,
    [switch]$SkipUnity,
    [switch]$Player,
    [switch]$Combo,
    [switch]$Bundle,
    [switch]$Install,
    [string]$Blender = $(if ($env:WORKSHOP_BLENDER) { $env:WORKSHOP_BLENDER } else { "$env:USERPROFILE\tools\blender\blender.exe" }),
    [string]$Unity = $(if ($env:WORKSHOP_UNITY) { $env:WORKSHOP_UNITY } else { "$env:USERPROFILE\tools\Unity\Editor\6000.0.75f1\Editor\Unity.exe" })
)
$ErrorActionPreference = 'Stop'
$workshop = Resolve-Path "$PSScriptRoot\..\.."
$axe = 'ecp_bone_greataxe'
$bake = Join-Path $PSScriptRoot $(if ($Player) { 'out\player' } else { 'out\blender' })
$blend = if ($Player) { 'greataxe_player' } else { 'headsman_moves' }

function Invoke-Unity([string]$method, [string[]]$more) {
    if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Unity is running; close it first (batch mode cannot share the project)' }
    $log = Join-Path $workshop 'out\headsman_unity.log'
    $argv = @('-batchmode', '-projectPath', "$workshop\unity", '-executeMethod', $method, '-logFile', $log) + $more
    $argv = $argv | ForEach-Object { if ($_ -match '\s') { "`"$_`"" } else { $_ } }
    Write-Host "Unity: $method"
    $process = Start-Process -FilePath $Unity -ArgumentList $argv -Wait -PassThru -NoNewWindow
    Get-Content $log | Where-Object { $_ -cmatch 'WORKSHOP|error CS' } | ForEach-Object { Write-Host "  $_" }
    if ($process.ExitCode -ne 0) { throw "Unity failed (exit $($process.ExitCode)); full log: $log" }
}

if (-not $SkipUnity) {
    $stage = Join-Path $workshop "unity\Assets\Bundles\ecp_headsman\$axe"
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    $dest = New-Item -ItemType Directory -Force $stage
    Copy-Item "$workshop\assets\$axe\out\$axe.fbx", "$workshop\assets\$axe\out\$axe.json", "$workshop\assets\$axe\out\${axe}_albedo.png" -Destination $dest
    if ($Probe) {
        Invoke-Unity 'Workshop.Headsman.HeadsmanProbe.Run' @()
        return
    }
    $stillsOut = Join-Path $PSScriptRoot 'out\stills'
    if ($Stills) {
        Invoke-Unity 'Workshop.Headsman.HeadsmanBuild.Run' @('-workshopStills', $stillsOut)
        return
    }
    if ($Bundle) {
        $icons = Join-Path $workshop "unity\Assets\Bundles\ecp_headsman\icons"
        $ErrorActionPreference = 'Continue'
        & $Blender --background "$workshop\assets\$axe\out\$axe.blend" --python-exit-code 1 --python "$PSScriptRoot\icons.py" 2>&1 |
            ForEach-Object { "$_" } | Where-Object { $_ -match 'WORKSHOP|Error|Traceback' } | ForEach-Object { Write-Host "  $_" }
        if ($LASTEXITCODE -ne 0) { throw 'Blender failed rendering the greataxe icons' }
        $ErrorActionPreference = 'Stop'
        # The whole weapon uses its authored icon; retain the rendered axehead loot icon.
        Copy-Item "$workshop\assets\$axe\ecp_greataxe_icon.png" -Destination "$PSScriptRoot\out\icons\ecp_greataxe_icon.png"
        New-Item -ItemType Directory -Force $icons | Out-Null
        Copy-Item "$PSScriptRoot\out\icons\*.png" -Destination $icons
        $bundles = Join-Path $workshop 'out\bundles\ecp_headsman'
        Invoke-Unity 'Workshop.Headsman.HeadsmanBuild.Run' @('-workshopBundle', $bundles)
        if ($Install) {
            $mod = Join-Path $workshop '..\EliteCreaturesPack\EliteCreaturesPack\assets\bundles'
            Copy-Item "$bundles\ecp_headsman.windows", "$bundles\ecp_headsman.linux" -Destination $mod
            Write-Host "Installed the bundles into $mod"
        }
        return
    }
    if (Test-Path $bake) { Remove-Item $bake -Recurse -Force }
    $method = if ($Player) { 'Workshop.Greataxe.GreataxePreview.Run' } else { 'Workshop.Headsman.HeadsmanBuild.Run' }
    $more = @('-workshopOut', $bake) + $(if ($Player -and $Combo) { @('-workshopCombo') } else { @() })
    Invoke-Unity $method $more
}

# Blender writes warnings to stderr, which 'Stop' would turn into a failure.
$ErrorActionPreference = 'Continue'
# The sounds first (sfx.py: our own, the game's being almost all bass), then the scene that places them.
& $Blender --background --factory-startup --python-exit-code 1 --python "$PSScriptRoot\sfx.py" -- (Join-Path $PSScriptRoot 'out\sfx') 2>&1 |
    ForEach-Object { "$_" } | Where-Object { $_ -match 'WORKSHOP sfx:|Error|Traceback' } | ForEach-Object { Write-Host "  $_" }
if ($LASTEXITCODE -ne 0) { throw 'Blender failed making the headsman sounds' }
# The same recipes as the mod's table (EliteCreaturesPack Headsman/Sound/HeadsmanSoundTable.cs).
& $Blender --background --factory-startup --python-exit-code 1 --python "$PSScriptRoot\sfx_table.py" 2>&1 |
    ForEach-Object { "$_" } | Where-Object { $_ -match 'WORKSHOP sfx table|Error|Traceback' } | ForEach-Object { Write-Host "  $_" }
if ($LASTEXITCODE -ne 0) { throw 'Blender failed writing the headsman sound table' }
& $Blender --background --factory-startup --python-exit-code 1 --python "$PSScriptRoot\blender_scene.py" -- --bake $bake --name $blend 2>&1 |
    ForEach-Object { "$_" } | Where-Object { $_ -match 'WORKSHOP|Error|Traceback' } | ForEach-Object { Write-Host "  $_" }
if ($LASTEXITCODE -ne 0) { throw 'Blender failed building the headsman scene' }
$ErrorActionPreference = 'Stop'
if ($Open) {
    Start-Process -FilePath $Blender -ArgumentList @("`"$bake\$blend.blend`"", '--python', "`"$PSScriptRoot\blender_play.py`"")
}

