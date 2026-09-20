<#
.SYNOPSIS
  Regenerates mod/thumbnail.png, the Steam Workshop preview image (1280x720).

.DESCRIPTION
  Rain World's in-game uploader rejects a thumbnail that is 1 MB or larger, or whose
  height/width is not 16:9 (0.5616 - 0.5634). This draws a plain placeholder with
  System.Drawing (no extra tools needed) that fits both rules. Replace mod/thumbnail.png
  with any 16:9 PNG under 1 MB (e.g. a screenshot of the Remix options screen) whenever you like -
  this script is only here so the placeholder can be regenerated.

  Run:  powershell -ExecutionPolicy Bypass -File workshop\make-thumbnail.ps1
#>
param(
    [string]$Output = (Join-Path (Split-Path -Parent $PSScriptRoot) "mod\thumbnail.png")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$w = 1280
$h = 720
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

# Background: dark blue-grey wash, a little lighter at the top like an overcast sky.
$rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 44, 56, 66)), ([System.Drawing.Color]::FromArgb(255, 14, 18, 24)), 90.0
$g.FillRectangle($bg, $rect)

# Rain: thin slanted streaks, seeded so the image is the same every run.
$rng = New-Object System.Random 312520
for ($i = 0; $i -lt 260; $i++) {
    $x = $rng.Next(-100, $w + 100)
    $y = $rng.Next(-60, $h)
    $len = $rng.Next(30, 110)
    $alpha = $rng.Next(12, 46)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb($alpha, 190, 210, 225)), 1.5
    $g.DrawLine($pen, $x, $y, $x - ($len * 0.22), $y + $len)
    $pen.Dispose()
}

# Sound waveform: mirrored bars around a centre line, tallest in the middle.
$barCount = 41
$barW = 14
$gap = 12
$totalW = $barCount * ($barW + $gap) - $gap
$startX = ($w - $totalW) / 2
$centerY = 250
$maxHalf = 120
for ($i = 0; $i -lt $barCount; $i++) {
    $t = ($i - ($barCount - 1) / 2) / (($barCount - 1) / 2)          # -1 .. 1
    $envelope = [Math]::Exp(-2.6 * $t * $t)                          # bell shape
    $wobble = 0.55 + 0.45 * [Math]::Abs([Math]::Sin($i * 1.7) * [Math]::Cos($i * 0.6))
    $half = [Math]::Max(7, $maxHalf * $envelope * $wobble)
    $x = $startX + $i * ($barW + $gap)
    $barRect = New-Object System.Drawing.RectangleF $x, ($centerY - $half), $barW, (2 * $half)
    $c1 = [System.Drawing.Color]::FromArgb(255, 255, 214, 130)
    $c2 = [System.Drawing.Color]::FromArgb(255, 230, 120, 70)
    $br = New-Object System.Drawing.Drawing2D.LinearGradientBrush $barRect, $c1, $c2, 90.0
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = $barW
    $path.AddArc($barRect.X, $barRect.Y, $r, $r, 180, 180)
    $path.AddArc($barRect.X, $barRect.Bottom - $r, $r, $r, 0, 180)
    $path.CloseFigure()
    $g.FillPath($br, $path)
    $path.Dispose()
    $br.Dispose()
}

# Title.
$sf = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$sf.LineAlignment = [System.Drawing.StringAlignment]::Center

$titleFont = New-Object System.Drawing.Font "Segoe UI Semibold", 92, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$subFont = New-Object System.Drawing.Font "Segoe UI", 34, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
$tagFont = New-Object System.Drawing.Font "Segoe UI Semibold", 26, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)

$shadow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(140, 0, 0, 0))
$white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 244, 246, 248))
$soft = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 176, 190, 202))
$accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 196, 110))

$titleRect = New-Object System.Drawing.RectangleF 0, 420, $w, 120
$shadowRect = New-Object System.Drawing.RectangleF 4, 424, $w, 120
$g.DrawString("CUSTOM SOUNDBOARD", $titleFont, $shadow, $shadowRect, $sf)
$g.DrawString("CUSTOM SOUNDBOARD", $titleFont, $white, $titleRect, $sf)

$subRect = New-Object System.Drawing.RectangleF 0, 545, $w, 50
$g.DrawString("Your own sounds for every moment in Rain World", $subFont, $soft, $subRect, $sf)

$tagRect = New-Object System.Drawing.RectangleF 0, 630, $w, 40
$g.DrawString("DEATH   *   JUMP   *   EAT   *   SPOTTED   *   SHELTER   *   AND MORE", $tagFont, $accent, $tagRect, $sf)

$g.Dispose()
$dir = Split-Path -Parent $Output
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$bmp.Save($Output, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

$size = (Get-Item $Output).Length
Write-Host ("Wrote {0} ({1}x{2}, {3:N0} bytes)" -f $Output, $w, $h, $size)
if ($size -ge 1000000) { Write-Warning "Larger than the 1 MB the Workshop uploader accepts." }
