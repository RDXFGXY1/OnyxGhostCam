<#
.SYNOPSIS
    Builds packaging/onyx.ico from image/onyx.png, recoloured to the Onyx palette
    (red ghost face on near-black) at all standard icon sizes.
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$src  = Join-Path $root 'image\onyx.png'
$out  = Join-Path $root 'packaging\onyx.ico'
New-Item -ItemType Directory -Force -Path (Split-Path $out) | Out-Null

$srcBmp = [System.Drawing.Bitmap]::FromFile($src)
$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.Clear([System.Drawing.Color]::FromArgb(255, 10, 10, 10))

    # Draw the source, then recolour opaque pixels to Onyx red.
    $tmp = New-Object System.Drawing.Bitmap $s, $s
    $tg = [System.Drawing.Graphics]::FromImage($tmp)
    $tg.InterpolationMode = 'HighQualityBicubic'
    $tg.DrawImage($srcBmp, 0, 0, $s, $s)
    $tg.Dispose()

    for ($y = 0; $y -lt $s; $y++) {
        for ($x = 0; $x -lt $s; $x++) {
            $p = $tmp.GetPixel($x, $y)
            # The source art is dark-on-transparent: treat dark+opaque as "ink".
            if ($p.A -gt 40 -and ($p.R + $p.G + $p.B) -lt 400) {
                $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, 200, 24, 24))
            }
        }
    }
    $tmp.Dispose()
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()
    $ms.Dispose(); $bmp.Dispose()
}
$srcBmp.Dispose()

# Assemble a PNG-compressed .ico container.
$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush(); $bw.Close(); $fs.Close()

Write-Host "icon written -> $out ($([math]::Round((Get-Item $out).Length/1KB)) KB)" -ForegroundColor Green
