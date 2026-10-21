<# Save generated icon masters and transparent inventory sizes from icons-generated.json. #>
param([string]$Manifest = "$PSScriptRoot/out/remake/icons-generated.json")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$entries = Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
$assetsRoot = Split-Path $PSScriptRoot -Parent
$report = @()
foreach ($entry in $entries) {
    $folder = Join-Path $assetsRoot $entry.asset
    $source = [System.Drawing.Image]::FromFile($entry.source)
    try {
        Copy-Item -LiteralPath $entry.source -Destination (Join-Path $folder "out/$($entry.icon)_master.png")
        foreach ($size in @(128,64)) {
            $bitmap = New-Object System.Drawing.Bitmap($size,$size,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.DrawImage($source,0,0,$size,$size)
                $destination = if ($size -eq 128) { Join-Path $folder "$($entry.icon).png" } else { Join-Path $folder "out/$($entry.icon)_64.png" }
                $bitmap.Save($destination,[System.Drawing.Imaging.ImageFormat]::Png)
                if ($size -eq 128) {
                    Copy-Item -LiteralPath $destination -Destination (Join-Path $folder "out/$($entry.icon).png")
                    $alphaPixels=0
                    $solidPixels=0
                    for ($y=0; $y -lt $size; $y++) {
                        for ($x=0; $x -lt $size; $x++) {
                            $alpha=$bitmap.GetPixel($x,$y).A
                            if ($alpha -eq 0) { $alphaPixels++ }
                            if ($alpha -gt 0) { $solidPixels++ }
                        }
                    }
                    if ($alphaPixels -lt 100 -or $solidPixels -lt 50) { throw "Invalid alpha or empty icon: $($entry.asset)" }
                    $report += [pscustomobject]@{asset=$entry.asset;size=$size;transparentPixels=$alphaPixels;visiblePixels=$solidPixels;file=$destination}
                }
            } finally { $graphics.Dispose(); $bitmap.Dispose() }
        }
    } finally { $source.Dispose() }
}
$report | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath "$PSScriptRoot/out/remake/icon-validation.json"
$report | Format-Table asset,size,transparentPixels,visiblePixels

# Review both shipped size and the game's common 64 px display size.
$columns=6
$rows=[int][Math]::Ceiling($entries.Count / $columns)
$sheet=New-Object System.Drawing.Bitmap(($columns*220),($rows*260))
$canvas=[System.Drawing.Graphics]::FromImage($sheet)
$font=New-Object System.Drawing.Font('Segoe UI',11)
$brush=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220,210,186))
try {
    $canvas.Clear([System.Drawing.Color]::FromArgb(38,35,31))
    for ($i=0; $i -lt $entries.Count; $i++) {
        $entry=$entries[$i]
        $folder=Join-Path $assetsRoot $entry.asset
        $x=($i % $columns)*220
        $y=[int][Math]::Floor($i / $columns)*260
        $large=[System.Drawing.Image]::FromFile((Join-Path $folder "$($entry.icon).png"))
        $small=[System.Drawing.Image]::FromFile((Join-Path $folder "out/$($entry.icon)_64.png"))
        try {
            $canvas.DrawImageUnscaled($large,($x+46),($y+10))
            $canvas.DrawImageUnscaled($small,($x+78),($y+145))
            $label=$entry.asset.Replace('ecp_skel_','').Replace('ecp_xbow_','').Replace('ecp_bone_','').Replace('_player','')
            $canvas.DrawString($label,$font,$brush,($x+55),($y+225))
        } finally { $large.Dispose(); $small.Dispose() }
    }
    $sheet.Save("$PSScriptRoot/out/remake/icons-review.png",[System.Drawing.Imaging.ImageFormat]::Png)
} finally { $canvas.Dispose(); $sheet.Dispose(); $font.Dispose(); $brush.Dispose() }
