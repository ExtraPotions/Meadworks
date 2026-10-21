# Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
param(
 [Parameter(Mandatory)][ValidateSet('Mantle','Inscribe','Bestiary','Larder','Shepherd')][string]$Product,
 [Parameter(Mandatory)][string]$UnityProject,
 [Parameter(Mandatory)][string]$UnityEditor,
 [string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$kitRoot=$PSScriptRoot
$productName=$Product.ToLowerInvariant()
$sourcePath=Join-Path $kitRoot ($productName+'/models.json')
$builderPath=Join-Path $kitRoot 'BuildCustomItems.cs'
$atlasPath=Join-Path $kitRoot 'textures/materials-atlas.png'
foreach($required in @($sourcePath,$builderPath,$atlasPath)) {
 if(!(Test-Path -LiteralPath $required -PathType Leaf)){throw ('Missing local source input: '+$required)}
}
$projectRoot=(Resolve-Path -LiteralPath $UnityProject).Path
$editorPath=(Resolve-Path -LiteralPath $UnityEditor).Path
$versionPath=Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'
if(!(Test-Path -LiteralPath (Join-Path $projectRoot 'Assets') -PathType Container) -or !(Test-Path -LiteralPath $versionPath -PathType Leaf)){throw 'Supply an existing Unity project; this helper does not create one.'}
if((Get-Content -LiteralPath $versionPath -Raw) -notmatch 'm_EditorVersion: 6000\.0\.75f1\s'){throw 'The supplied project must use the qualified Unity 6000.0.75f1 editor.'}
if(Test-Path -LiteralPath (Join-Path $projectRoot 'Temp/UnityLockfile')){throw 'Close this project in Unity before the batch rebuild.'}
if(Get-Process -Name valheim,valheim_server -ErrorAction SilentlyContinue){throw 'Preserve running Valheim sessions; close them before using the heavy-job lane.'}
if(!$OutputDirectory){$OutputDirectory=Join-Path $kitRoot ('rebuilt/'+$productName+'-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))}
$outputRoot=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $outputRoot) -and @(Get-ChildItem -LiteralPath $outputRoot -Force).Count){throw 'Choose a new or empty output directory; prior exports are preserved.'}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$source=Get-Content -LiteralPath $sourcePath -Raw | ConvertFrom-Json
if($source.schema -ne 1 -or $source.product -ne $Product -or $source.units -ne 'meters'){throw 'The local source does not match the requested product/schema.'}
$sourceHash=(Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
$builderHash=(Get-FileHash -LiteralPath $builderPath -Algorithm SHA256).Hash
$atlasHash=(Get-FileHash -LiteralPath $atlasPath -Algorithm SHA256).Hash
$editorFolder=Join-Path $projectRoot 'Assets/Editor/CustomItems'
$editorTarget=Join-Path $editorFolder 'BuildCustomItems.cs'
$metaTarget=$editorTarget+'.meta'
$hadBuilder=Test-Path -LiteralPath $editorTarget -PathType Leaf
$hadMeta=Test-Path -LiteralPath $metaTarget -PathType Leaf
[byte[]]$priorBuilder=@()
if($hadBuilder){$priorBuilder=[IO.File]::ReadAllBytes($editorTarget)}
$ownedProcess=$null
try {
 New-Item -ItemType Directory -Path $editorFolder -Force | Out-Null
 Copy-Item -LiteralPath $builderPath -Destination $editorTarget
 $arguments=@('-batchmode','-projectPath',('"'+$projectRoot+'"'),'-executeMethod','MeadWorks.CustomItems.BuildCustomItems.Build','-customItemsSource',('"'+$sourcePath+'"'),'-customItemsOut',('"'+$outputRoot+'"'),'-logFile',('"'+(Join-Path $outputRoot 'unity.log')+'"'))
 $ownedProcess=Start-Process -FilePath $editorPath -WorkingDirectory $projectRoot -ArgumentList $arguments -WindowStyle Hidden -PassThru
 $ownedStart=$ownedProcess.StartTime.ToUniversalTime()
 if(!$ownedProcess.WaitForExit(600000)){throw 'The owned Unity rebuild exceeded ten minutes; retain its output/log for diagnosis.'}
 if($ownedProcess.ExitCode -ne 0){throw ('Unity rebuild failed with exit '+$ownedProcess.ExitCode+'; inspect '+(Join-Path $outputRoot 'unity.log'))}
 $receipt=Get-Content -LiteralPath (Join-Path $outputRoot 'build-result.json') -Raw | ConvertFrom-Json
 if($receipt.product -ne $Product -or $receipt.assets -ne $source.assets.Count -or !$receipt.modelRenderedIcons -or ($receipt.platforms -join ',') -ne 'windows,linux' -or $receipt.sourceSha256 -ne $sourceHash -or $receipt.builderSha256 -ne $builderHash -or $receipt.atlasSha256 -ne $atlasHash){throw 'The build receipt does not match the local inputs or required exports.'}
 foreach($platform in @('windows','linux')) {
  $bundle=Join-Path $outputRoot ('mw_'+$productName+'_items.'+$platform)
  if(!(Test-Path -LiteralPath $bundle -PathType Leaf) -or (Get-Item -LiteralPath $bundle).Length -lt 1000){throw ('Missing bundle: '+$bundle)}
 }
 foreach($asset in $source.assets) {
  $icon=Join-Path $outputRoot ($asset.id+'_icon.png')
  if(!(Test-Path -LiteralPath $icon -PathType Leaf)){throw ('Missing rendered icon: '+$icon)}
 }
 Write-Output ('Rebuilt '+$Product+': '+$outputRoot)
} finally {
 if($ownedProcess -and !$ownedProcess.HasExited) {
  $running=Get-CimInstance Win32_Process -Filter ('ProcessId='+$ownedProcess.Id)
  if(!$running -or $running.ExecutablePath -ne $editorPath -or $ownedProcess.StartTime.ToUniversalTime() -ne $ownedStart){throw 'Cannot verify the remaining process owner; builder is retained for inspection.'}
  Stop-Process -Id $ownedProcess.Id
  $ownedProcess.WaitForExit()
 }
 if($hadBuilder){[IO.File]::WriteAllBytes($editorTarget,$priorBuilder)}elseif(Test-Path -LiteralPath $editorTarget){Remove-Item -LiteralPath $editorTarget}
 if(!$hadMeta -and (Test-Path -LiteralPath $metaTarget)){Remove-Item -LiteralPath $metaTarget}
}
