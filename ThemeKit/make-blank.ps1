$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Join-Path $PSScriptRoot 'Blank'
$assets=[ordered]@{
 'background.png'=@(1000,670);'sidebar.png'=@(200,636);'header.png'=@(1000,34)
 'panel.png'=@(744,608);'card.png'=@(744,360);'logo.png'=@(180,100)
 'icon.png'=@(38,38);'decoration.png'=@(85,55);'hero.png'=@(240,240)
 'buttons/normal.png'=@(220,46);'buttons/hover.png'=@(220,46)
 'buttons/pressed.png'=@(220,46);'buttons/disabled.png'=@(220,46)
 'navigation/normal.png'=@(164,40);'navigation/hover.png'=@(164,40)
 'navigation/pressed.png'=@(164,40);'navigation/disabled.png'=@(164,40)
 'ribbon.png'=@(374,52);'steam-card.png'=@(176,72);'content-card.png'=@(744,496);'mod-row.png'=@(710,70)
 'versions/background.png'=@(720,630)
 'versions/cards/normal.png'=@(160,49);'versions/cards/hover.png'=@(160,49);'versions/cards/selected.png'=@(160,49)
 'versions/buttons/normal.png'=@(166,45);'versions/buttons/hover.png'=@(166,45);'versions/buttons/pressed.png'=@(166,45)
}
foreach($entry in $assets.GetEnumerator()){
 $path=Join-Path $root $entry.Key
 New-Item -ItemType Directory -Force -Path (Split-Path -Parent $path) | Out-Null
 $bmp=New-Object System.Drawing.Bitmap($entry.Value[0],$entry.Value[1])
 $bmp.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
 $bmp.Dispose()
}
$guide=New-Object System.Drawing.Bitmap(1000,670)
$g=[System.Drawing.Graphics]::FromImage($guide)
$g.Clear([System.Drawing.Color]::FromArgb(24,32,43))
$font=New-Object System.Drawing.Font('Segoe UI',15)
$white=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
function Box([int]$x,[int]$y,[int]$w,[int]$h,[string]$text,[int]$r,[int]$gr,[int]$b){
 $brush=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($r,$gr,$b))
 $g.FillRectangle($brush,$x,$y,$w,$h)
 $g.DrawString($text,$font,$white,($x+8),($y+8))
 $brush.Dispose()
}
Box 0 0 1000 34 'Header 1000 x 34' 48 65 90
Box 0 34 200 636 'Sidebar 200 x 636' 41 57 81
Box 228 50 744 608 'Panel 744 x 608' 42 78 102
Box 228 130 744 360 'Card 744 x 360' 35 49 71
Box 241 144 374 52 'Ribbon 374 x 52' 64 42 97
Box 716 190 240 240 'Hero 240 x 240' 95 65 140
Box 17 65 164 40 'Navigation 164 x 40' 95 65 140
Box 12 586 176 72 'Steam 176 x 72' 41 90 80
Box 517 571 220 46 'Choose version' 170 84 69
Box 747 571 220 46 'Play' 170 84 69
$guide.Save((Join-Path $PSScriptRoot 'layout-guide.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$guide.Dispose();$font.Dispose();$white.Dispose()
$versionGuide=New-Object System.Drawing.Bitmap(720,630)
$vg=[System.Drawing.Graphics]::FromImage($versionGuide)
$vg.Clear([System.Drawing.Color]::FromArgb(10,11,24))
$textFont=New-Object System.Drawing.Font('Segoe UI',12)
$labelBrush=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$cardBrush=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(30,26,52))
$vg.DrawString('Version catalog 720 x 630', $textFont,$labelBrush,23,25)
for($row=0;$row -lt 6;$row++){for($column=0;$column -lt 4;$column++){$x=28+$column*168;$y=164+$row*56;$vg.FillRectangle($cardBrush,$x,$y,160,49)}}
$vg.FillRectangle($cardBrush,529,555,166,45)
$vg.DrawString('Select button 166 x 45',$textFont,$labelBrush,480,523)
$versionGuide.Save((Join-Path $PSScriptRoot 'version-layout-guide.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$vg.Dispose();$versionGuide.Dispose();$textFont.Dispose();$labelBrush.Dispose();$cardBrush.Dispose()
$modGuide=New-Object System.Drawing.Bitmap(710,70)
$mg=[System.Drawing.Graphics]::FromImage($modGuide)
$mg.Clear([System.Drawing.Color]::FromArgb(22,24,41))
$iconBrush=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(85,64,150))
$font=New-Object System.Drawing.Font('Segoe UI',12)
$white=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$mg.FillRectangle($iconBrush,16,11,48,48)
$mg.DrawString('Icon 48 x 48',$font,$white,75,7)
$mg.DrawString('Name / author / version: text is drawn by the launcher',$font,$white,75,35)
$modGuide.Save((Join-Path $PSScriptRoot 'mod-row-layout-guide.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$mg.Dispose();$modGuide.Dispose();$iconBrush.Dispose();$font.Dispose();$white.Dispose()
