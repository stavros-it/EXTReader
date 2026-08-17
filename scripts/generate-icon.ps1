Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Drawing.Primitives

$outDir = "$PSScriptRoot\..\src\EXTReader"
$icoPath = "$outDir\app.ico"
$pngPath = "$outDir\app_preview.png"

$sizes = @(256, 48, 32, 16)
$bitmaps = @{}

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    if ($size -ge 48) {
        $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    }
    $g.Clear([System.Drawing.Color]::Transparent)

    # scale factor relative to 256
    $s = $size / 256.0

    # ---- 1. Rounded square background (dark teal gradient) ----
    $corner = 48 * $s
    $bgRect = New-Object System.Drawing.RectangleF(0, 0, $size, $size)
    $bgPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bgPath.AddArc($bgRect.X, $bgRect.Y, $corner, $corner, 180, 90)
    $bgPath.AddArc($bgRect.Right - $corner, $bgRect.Y, $corner, $corner, 270, 90)
    $bgPath.AddArc($bgRect.Right - $corner, $bgRect.Bottom - $corner, $corner, $corner, 0, 90)
    $bgPath.AddArc($bgRect.X, $bgRect.Bottom - $corner, $corner, $corner, 90, 90)
    $bgPath.CloseFigure()

    $gradTop = [System.Drawing.Color]::FromArgb(255, 28, 45, 70)
    $gradBot = [System.Drawing.Color]::FromArgb(255, 12, 75, 95)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0, 0)),
        (New-Object System.Drawing.PointF(0, $size)),
        $gradTop, $gradBot
    )
    $g.FillPath($brush, $bgPath)
    $brush.Dispose()

    # ---- 2. Disk cylinder shape (white/light) ----
    $diskLeft = 56 * $s
    $diskTop = 60 * $s
    $diskW = 144 * $s
    $diskH = 110 * $s
    $diskEllipseH = 26 * $s

    $diskColor = [System.Drawing.Color]::FromArgb(245, 235, 245, 255)
    $diskShadow = [System.Drawing.Color]::FromArgb(220, 180, 200, 230)

    # Cylinder body (rectangle with top/bottom ellipses)
    $bodyRect = New-Object System.Drawing.RectangleF($diskLeft, $diskTop + $diskEllipseH/2, $diskW, $diskH)
    $bodyBrush = New-Object System.Drawing.SolidBrush($diskColor)
    $g.FillRectangle($bodyBrush, $bodyRect)

    # Bottom ellipse
    $bottomRect = New-Object System.Drawing.RectangleF($diskLeft, $diskTop + $diskH - $diskEllipseH/2 + $diskEllipseH/2, $diskW, $diskEllipseH)

    # Fix: bottom ellipse should be at bottom of body
    $bottomRect = New-Object System.Drawing.RectangleF($diskLeft, $diskTop + $diskH, $diskW, $diskEllipseH)
    $g.FillEllipse($bodyBrush, $bottomRect)

    # Top ellipse (slightly darker - the "top" of the disk)
    $topRect = New-Object System.Drawing.RectangleF($diskLeft, $diskTop, $diskW, $diskEllipseH)
    $topBrush = New-Object System.Drawing.SolidBrush($diskShadow)
    $g.FillEllipse($topBrush, $topRect)
    $topBrush.Dispose()
    $bodyBrush.Dispose()

    # ---- 3. Green accent stripe on the disk (Linux green) ----
    $stripeY = $diskTop + $diskEllipseH + 18 * $s
    $stripeH = 14 * $s
    $stripeRect = New-Object System.Drawing.RectangleF($diskLeft, $stripeY, $diskW, $stripeH)
    $greenColor = [System.Drawing.Color]::FromArgb(255, 0, 200, 83)
    $greenBrush = New-Object System.Drawing.SolidBrush($greenColor)
    $g.FillRectangle($greenBrush, $stripeRect)

    # ---- 4. "ext" text on the green stripe (only for large sizes) ----
    if ($size -ge 32) {
        $fontSize = [int]($size * 0.14)
        $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        $sf = New-Object System.Drawing.StringFormat
        $sf.Alignment = [System.Drawing.StringAlignment]::Center
        $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
        $textRect = New-Object System.Drawing.RectangleF($diskLeft, $stripeY - 1, $diskW, $stripeH)
        $textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
        $g.DrawString("ext", $font, $textBrush, $textRect, $sf)
        $font.Dispose()
        $textBrush.Dispose()
    }

    $greenBrush.Dispose()
    $g.Dispose()
    $bitmaps[$size] = $bmp
}

# Save preview PNG (256x256)
$bitmaps[256].Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output "Preview saved: $pngPath"

# ---- Write ICO file (multi-size, PNG-encoded for 256, BMP for smaller) ----
$ms = New-Object System.IO.MemoryStream

# ICO header (6 bytes)
$writer = New-Object System.IO.BinaryWriter($ms)
$writer.Write([UInt16]0)        # reserved
$writer.Write([UInt16]1)        # type = icon
$writer.Write([UInt16]$sizes.Count)  # count

# Directory entries (16 bytes each)
$dataOffset = 6 + ($sizes.Count * 16)
$entries = @()

foreach ($size in $sizes) {
    $bmp = $bitmaps[$size]
    $pngMs = New-Object System.IO.MemoryStream
    $bmp.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytes = $pngMs.ToArray()
    $pngMs.Close()
    $entries += ,@{ size = $size; bytes = $pngBytes; offset = $dataOffset }
    $dataOffset += $pngBytes.Length
}

foreach ($entry in $entries) {
    $s = $entry.size
    if ($s -ge 256) { $writer.Write([byte]0) } else { $writer.Write([byte]$s) }
    if ($s -ge 256) { $writer.Write([byte]0) } else { $writer.Write([byte]$s) }
    $writer.Write([byte]0)                          # color count (0 = no palette)
    $writer.Write([byte]0)                          # reserved
    $writer.Write([UInt16]1)                        # color planes
    $writer.Write([UInt16]32)                       # bits per pixel
    $writer.Write([UInt32]$entry.bytes.Length)      # size of data
    $writer.Write([UInt32]$entry.offset)             # offset
}

foreach ($entry in $entries) {
    $writer.Write($entry.bytes)
}

[System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())
$writer.Close()
$ms.Close()

foreach ($size in $sizes) { $bitmaps[$size].Dispose() }

Write-Output "Icon saved: $icoPath ($((Get-Item $icoPath).Length) bytes)"
Write-Output "Sizes: $($sizes -join ', ')"
