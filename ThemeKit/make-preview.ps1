param([ValidateSet('GraphicDemo','Default')][string]$Theme='GraphicDemo')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=if($Theme -eq 'Default'){Join-Path (Split-Path -Parent $PSScriptRoot) 'ThemeAssets/Default'}else{Join-Path $PSScriptRoot 'GraphicDemo'}
$canvas=New-Object System.Drawing.Bitmap(1000,670)
$g=[System.Drawing.Graphics]::FromImage($canvas)
function Layer([string]$relative,[int]$x,[int]$y,[int]$w,[int]$h){
 $path=Join-Path $root $relative
 $img=[System.Drawing.Image]::FromFile($path)
 $g.DrawImage($img,$x,$y,$w,$h)
 $img.Dispose()
}
Layer 'background.png' 0 0 1000 670
Layer 'sidebar.png' 0 34 200 636
Layer 'header.png' 0 0 1000 34
Layer 'panel.png' 228 50 744 608
Layer 'card.png' 228 130 744 360
Layer 'logo.png' 12 46 180 100
Layer 'icon.png' 9 8 17 17
Layer 'decoration.png' 900 5 28 18
Layer 'hero.png' 700 210 240 240
for($i=0;$i -lt 7;$i++){Layer 'navigation/normal.png' 17 (138+$i*50) 164 40}
Layer 'buttons/normal.png' 494 570 220 46
Layer 'buttons/normal.png' 728 570 220 46
$font=New-Object System.Drawing.Font('Segoe UI',16,[System.Drawing.FontStyle]::Bold)
$white=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$g.DrawString('Geometry Dash Launcher',$font,$white,34,5)
$g.DrawString('Geometry Dash',$font,$white,228,65)
$g.DrawString('Управление игрой',$font,$white,260,220)
$g.DrawString('Папка игры',$font,$white,525,577)
$g.DrawString('Играть',$font,$white,795,577)
$canvas.Save((Join-Path $PSScriptRoot "$Theme-preview.png"),[System.Drawing.Imaging.ImageFormat]::Png)
$font.Dispose();$white.Dispose();$g.Dispose();$canvas.Dispose()
