# Icône ScreenLink comme SDRLink : carré bleu marine arrondi, « SCREEN » blanc en haut, « LINK » orange dessous.
# Fabrique pc\ScreenLink.ico et les PNG de l'application Android.
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot
function Make($s) {
    $bmp = New-Object Drawing.Bitmap $s, $s
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([Drawing.Color]::Transparent)
    $r = [Math]::Max(2, [int]($s * 0.18)); $w = $s - 1
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $r * 2, $r * 2, 180, 90); $path.AddArc($w - $r * 2, 0, $r * 2, $r * 2, 270, 90)
    $path.AddArc($w - $r * 2, $w - $r * 2, $r * 2, $r * 2, 0, 90); $path.AddArc(0, $w - $r * 2, $r * 2, $r * 2, 90, 90)
    $path.CloseFigure()
    $bg = New-Object Drawing.Drawing2D.LinearGradientBrush (New-Object Drawing.Rectangle 0, 0, $s, $s), ([Drawing.Color]::FromArgb(20, 40, 150)), ([Drawing.Color]::FromArgb(0, 0, 90)), 90
    $g.FillPath($bg, $path)
    $sf = New-Object Drawing.StringFormat
    $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'; $sf.FormatFlags = 'NoWrap, NoClip'; $sf.Trimming = 'None'
    function Fit($text, $em, $maxW) {
        do { $f = New-Object Drawing.Font 'Arial', ([float]$em), ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel); $em *= 0.95 }
        while ($g.MeasureString($text, $f, 10000, [Drawing.StringFormat]::GenericTypographic).Width -gt $maxW)
        $f
    }
    $f1 = Fit 'SCREEN' ($s * 0.34) ($s * 0.86)
    $f2 = Fit 'LINK' ($s * 0.30) ($s * 0.72)
    $g.DrawString('SCREEN', $f1, [Drawing.Brushes]::White, (New-Object Drawing.RectangleF 0, ([float]($s * 0.10)), $s, ([float]($s * 0.40))), $sf)
    $g.DrawString('LINK', $f2, (New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 165, 40))), (New-Object Drawing.RectangleF 0, ([float]($s * 0.50)), $s, ([float]($s * 0.38))), $sf)
    $g.Dispose()
    $bmp
}
# .ico
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngs = @()
foreach ($s in $sizes) { $b = Make $s; $ms = New-Object IO.MemoryStream; $b.Save($ms, [Drawing.Imaging.ImageFormat]::Png); $pngs += , $ms.ToArray(); $b.Dispose() }
$fs = [IO.File]::Create((Join-Path $root 'pc\ScreenLink.ico')); $bw = New-Object IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$off = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $bw.Write([byte]$sz); $bw.Write([byte]$sz); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$off); $off += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Close()
# Android
$res = Join-Path $root 'android\app\src\main\res'
foreach ($d in @(@('mdpi', 48), @('hdpi', 72), @('xhdpi', 96), @('xxhdpi', 144), @('xxxhdpi', 192))) {
    $dir = Join-Path $res ("mipmap-" + $d[0]); New-Item -ItemType Directory -Force $dir | Out-Null
    $b = Make $d[1]; $b.Save((Join-Path $dir 'ic_launcher.png'), [Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
}
"OK"
