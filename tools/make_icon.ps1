# Draws SmartKeyboard.ico, the icon on the exe, the desktop shortcut and the
# taskbar. Run it again after changing the design:
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\make_icon.ps1
#
# The design is the tray icon's (TrayController.BuildIcon), scaled up: an
# orange keyboard with three light keys and a space bar, in the brand colours.
# Every size is drawn fresh rather than shrunk from the largest, so the small
# ones stay crisp.
#
# An .ico is a small header, a table of entries, then one image per size.
# Each image is stored as a PNG, which Windows has read inside .ico files
# since Vista. The file is written with .NET, never with PowerShell's ">",
# because that turns binary data into text.

param(
    [string]$Out = (Join-Path $PSScriptRoot "..\src\SmartKeyboard.App\Assets\SmartKeyboard.ico")
)

Add-Type -AssemblyName System.Drawing

$orange = [System.Drawing.ColorTranslator]::FromHtml("#d97757")
$light = [System.Drawing.ColorTranslator]::FromHtml("#faf9f5")
$sizes = @(16, 24, 32, 48, 64, 256)

# A rectangle with rounded corners, or a plain one when the radius is tiny.
function New-RoundedPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    if ($r -lt 1) {
        $path.AddRectangle((New-Object System.Drawing.RectangleF($x, $y, $w, $h)))
        return $path
    }
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

# Draws one size and returns it as PNG bytes.
function New-IconPng([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # The tray design is laid out on a 32 pixel grid.
    $k = $size / 32.0
    $round = if ($size -ge 32) { 1 } else { 0 }

    $body = New-Object System.Drawing.SolidBrush($orange)
    $keys = New-Object System.Drawing.SolidBrush($light)

    $g.FillPath($body, (New-RoundedPath (1 * $k) (6 * $k) (30 * $k) (20 * $k) (3.5 * $k * $round)))

    foreach ($key in @(@(5, 11, 6, 5), @(13, 11, 6, 5), @(21, 11, 6, 5), @(8, 19, 16, 4))) {
        $g.FillPath($keys, (New-RoundedPath ($key[0] * $k) ($key[1] * $k) ($key[2] * $k) ($key[3] * $k) (1 * $k * $round)))
    }

    $g.Dispose()
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    return , $stream.ToArray()
}

$images = @()
foreach ($size in $sizes) {
    $images += , (New-IconPng $size)
}

$file = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($file)

# Header: reserved, type 1 (icon), number of images.
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$sizes.Count)

# One 16 byte entry per image. A size of 256 is written as 0.
$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $edge = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([Byte]$edge)
    $writer.Write([Byte]$edge)
    $writer.Write([Byte]0)      # no palette
    $writer.Write([Byte]0)      # reserved
    $writer.Write([UInt16]1)    # colour planes
    $writer.Write([UInt16]32)   # bits per pixel
    $writer.Write([UInt32]$images[$i].Length)
    $writer.Write([UInt32]$offset)
    $offset += $images[$i].Length
}

foreach ($image in $images) {
    $writer.Write($image)
}

$writer.Flush()
$bytes = $file.ToArray()
$target = [System.IO.Path]::GetFullPath($Out)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($target)) | Out-Null
[System.IO.File]::WriteAllBytes($target, $bytes)
$writer.Dispose()

"Wrote $target ($($bytes.Length) bytes, sizes $($sizes -join ', '))"
