param([switch]$SkipBlender, [switch]$References, [switch]$Animation)
$ErrorActionPreference = 'Stop'
$workshop = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$out = Join-Path $PSScriptRoot 'out'
New-Item -ItemType Directory -Force $out | Out-Null
$blender = Join-Path $env:USERPROFILE 'tools\blender\blender.exe'
if ($References) {
    python "$PSScriptRoot\reference_audit.py"
    if ($LASTEXITCODE -ne 0) { throw 'Reference inventory failed' }
    & $blender --background --factory-startup --python-exit-code 1 --python "$PSScriptRoot\reference_models.py" *> "$out\references.log"
    if ($LASTEXITCODE -ne 0) { throw 'Reference rendering failed' }
}
if (-not $SkipBlender) {
    & $blender --background --factory-startup --python-exit-code 1 --python "$PSScriptRoot\build.py" *> "$out\build.log"
    if ($LASTEXITCODE -ne 0) { throw "Blender failed; see $out\build.log" }
}
& $blender --background --factory-startup --python-exit-code 1 --python "$PSScriptRoot\validate.py" *> "$out\validation.log"
if ($LASTEXITCODE -ne 0) { throw "Validation failed; see $out\validation.log" }
if ($Animation) {
    & $blender --background --factory-startup --python-exit-code 1 --python "$PSScriptRoot\animate.py" *> "$out\animation.log"
    if ($LASTEXITCODE -ne 0) { throw "Animation failed; see $out\animation.log" }
}
python "$PSScriptRoot\presentation.py"
if ($LASTEXITCODE -ne 0) { throw 'Presentation failed' }
$names = 'ecp_bone_crossbow','ecp_bone_crossbow_unloaded','ecp_bone_quarrel'
foreach ($name in $names) {
    $source = Join-Path $workshop "assets\$name\out"
    $dest = Join-Path $workshop "unity\Assets\Bundles\ecp_bone_crossbow_set\$name"
    New-Item -ItemType Directory -Force $dest | Out-Null
    Copy-Item -LiteralPath "$source\$name.fbx","$source\$name.json","$source\${name}_albedo.png","$source\${name}_icon.png" -Destination $dest -Force
}
$unity = Join-Path $env:USERPROFILE 'tools\Unity\Editor\6000.0.75f1\Editor\Unity.exe'
$arguments = @('-batchmode','-nographics','-quit','-projectPath',"`"$workshop\unity`"",'-executeMethod','Workshop.BoneCrossbowBuild.Run','-logFile',"`"$out\unity.log`"")
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Unity failed; see $out\unity.log" }
Get-Content "$out\unity_validation.txt"
