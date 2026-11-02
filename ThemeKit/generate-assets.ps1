$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$project=Split-Path -Parent $PSScriptRoot
function Color([string]$hex){return [System.Drawing.ColorTranslator]::FromHtml($hex)}
function Make([string]$path,[int]$w,[int]$h,[string]$fill,[string]$accent,[string]$label){
 New-Item -ItemType Directory -Force -Path (Split-Path -Parent $path) | Out-Null
 $bmp=New-Object System.Drawing.Bitmap($w,$h)
 $g=[System.Drawing.Graphics]::FromImage($bmp)
 $g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $g.Clear((Color $fill))
 $pen=New-Object System.Drawing.Pen((Color $accent),1)
 $file=[IO.Path]::GetFileName($path)
 if($file -notin @('background.png','sidebar.png','header.png','logo.png','panel.png')){$g.DrawRectangle($pen,1,1,($w-3),($h-3))}
 if($file -eq 'background.png'){
  $gridPen=New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(26,115,99,220),1)
  for($x=0;$x -lt $w;$x+=80){$g.DrawLine($gridPen,$x,($h-95),($x-220),$h)}
  for($y=$h-100;$y -lt $h;$y+=22){$g.DrawLine($gridPen,0,$y,$w,$y)}
  $gridPen.Dispose()
 }
 if($file -eq 'header.png' -or $file -eq 'sidebar.png'){$g.DrawLine($pen,0,($h-1),$w,($h-1))}
 if($label){
  $size=[Math]::Max(12,[Math]::Min(25,[int]($h/3)))
  $font=New-Object System.Drawing.Font('Segoe UI',$size,[System.Drawing.FontStyle]::Bold)
  $brush=New-Object System.Drawing.SolidBrush((Color $accent))
  $g.DrawString($label,$font,$brush,10,[Math]::Max(2,[int](($h-$size*1.5)/2)))
  $font.Dispose();$brush.Dispose()
 }
 if($file -eq 'logo.png'){
  $big=New-Object System.Drawing.Font('Segoe UI',39,[System.Drawing.FontStyle]::Bold)
  $small=New-Object System.Drawing.Font('Segoe UI',13,[System.Drawing.FontStyle]::Bold)
  $sub=New-Object System.Drawing.Font('Segoe UI',9)
  $purple=New-Object System.Drawing.SolidBrush((Color $accent))
  $light=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
  $g.DrawString('»',$big,$purple,0,12)
  $g.DrawString('Geometry Dash',$small,$light,52,23)
  $g.DrawString('Launcher',$sub,$light,53,47)
  $big.Dispose();$small.Dispose();$sub.Dispose();$purple.Dispose();$light.Dispose()
 }
 $bmp.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
 $pen.Dispose();$g.Dispose();$bmp.Dispose()
}
function Hero([string]$path,[string]$accent){
 $bmp=New-Object System.Drawing.Bitmap(240,240)
 $g=[System.Drawing.Graphics]::FromImage($bmp)
 $g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $g.Clear([System.Drawing.Color]::Transparent)
 $circle=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(55,100,76,200))
 $g.FillEllipse($circle,5,5,230,230)
 $top=[System.Drawing.Point[]]@([System.Drawing.Point]::new(45,70),[System.Drawing.Point]::new(155,62),[System.Drawing.Point]::new(209,94),[System.Drawing.Point]::new(96,101))
 $left=[System.Drawing.Point[]]@([System.Drawing.Point]::new(45,70),[System.Drawing.Point]::new(96,101),[System.Drawing.Point]::new(96,210),[System.Drawing.Point]::new(45,180))
 $right=[System.Drawing.Point[]]@([System.Drawing.Point]::new(96,101),[System.Drawing.Point]::new(209,94),[System.Drawing.Point]::new(197,187),[System.Drawing.Point]::new(96,210))
 $a=New-Object System.Drawing.SolidBrush((Color $accent));$b=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(116,92,222));$c=New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(76,59,173))
 $g.FillPolygon($a,$top);$g.FillPolygon($b,$left);$g.FillPolygon($c,$right)
 $bmp.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
 $circle.Dispose();$a.Dispose();$b.Dispose();$c.Dispose();$g.Dispose();$bmp.Dispose()
}
function Ribbon([string]$path,[string]$fill){
 $bmp=New-Object System.Drawing.Bitmap(374,52)
 $g=[System.Drawing.Graphics]::FromImage($bmp)
 $g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $g.Clear([System.Drawing.Color]::Transparent)
 $brush=New-Object System.Drawing.SolidBrush((Color $fill))
 $points=[System.Drawing.Point[]]@([System.Drawing.Point]::new(0,0),[System.Drawing.Point]::new(373,0),[System.Drawing.Point]::new(373,36),[System.Drawing.Point]::new(322,51),[System.Drawing.Point]::new(0,51))
 $g.FillPolygon($brush,$points)
 $bmp.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
 $brush.Dispose();$g.Dispose();$bmp.Dispose()
}
function Theme([string]$folder,[string]$name,[string]$base,[string]$panel,[string]$accent,[string]$hover,[string]$pressed){
 New-Item -ItemType Directory -Force -Path $folder | Out-Null
 Make (Join-Path $folder 'background.png') 1000 670 $base $accent ''
 Make (Join-Path $folder 'sidebar.png') 200 636 $panel $accent ''
 Make (Join-Path $folder 'header.png') 1000 34 $panel $accent ''
 Make (Join-Path $folder 'panel.png') 744 608 $base $accent ''
 Make (Join-Path $folder 'card.png') 744 360 $panel $accent ''
 Make (Join-Path $folder 'logo.png') 180 100 $panel $accent ''
 Make (Join-Path $folder 'icon.png') 38 38 $accent '#FFFFFF' 'G'
 Make (Join-Path $folder 'decoration.png') 85 55 $panel $accent '///'
 $character=Join-Path $PSScriptRoot 'OriginalArt/puffi chibi.png'
 if(Test-Path -LiteralPath $character){Copy-Item -LiteralPath $character -Destination (Join-Path $folder 'hero.png') -Force}else{Hero (Join-Path $folder 'hero.png') $accent}
 Copy-Item -LiteralPath (Join-Path $project 'Launcher.UI/Assets/awee.wav') -Destination (Join-Path $folder 'hero.wav') -Force
 Make (Join-Path $folder 'buttons/normal.png') 220 46 $accent '#FFFFFF' ''
 Make (Join-Path $folder 'buttons/hover.png') 220 46 $hover '#FFFFFF' ''
 Make (Join-Path $folder 'buttons/pressed.png') 220 46 $pressed '#FFFFFF' ''
 Make (Join-Path $folder 'buttons/disabled.png') 220 46 '#59606E' '#C8CDD5' ''
 Make (Join-Path $folder 'navigation/normal.png') 164 40 $panel $accent ''
 Make (Join-Path $folder 'navigation/hover.png') 164 40 $hover '#FFFFFF' ''
 Make (Join-Path $folder 'navigation/pressed.png') 164 40 $pressed '#FFFFFF' ''
 Make (Join-Path $folder 'navigation/disabled.png') 164 40 '#59606E' '#C8CDD5' ''
 if($name -eq 'GraphicDemo'){
  Ribbon (Join-Path $folder 'ribbon.png') $accent
  Make (Join-Path $folder 'steam-card.png') 176 72 $panel $accent ''
  Make (Join-Path $folder 'content-card.png') 744 496 $panel $accent ''
  Make (Join-Path $folder 'mod-row.png') 710 70 $panel $accent ''
  Make (Join-Path $folder 'versions/background.png') 720 630 $base $accent ''
  Make (Join-Path $folder 'versions/cards/normal.png') 160 49 $panel $accent ''
  Make (Join-Path $folder 'versions/cards/hover.png') 160 49 $hover '#FFFFFF' ''
  Make (Join-Path $folder 'versions/cards/selected.png') 160 49 $pressed '#FFFFFF' ''
  Make (Join-Path $folder 'versions/buttons/normal.png') 166 45 $accent '#FFFFFF' ''
  Make (Join-Path $folder 'versions/buttons/hover.png') 166 45 $hover '#FFFFFF' ''
  Make (Join-Path $folder 'versions/buttons/pressed.png') 166 45 $pressed '#FFFFFF' ''
 }
 $extraImage=if($name -eq 'GraphicDemo'){@{ribbon='ribbon.png';steam='steam-card.png';content='content-card.png';versionBackground='versions/background.png';versionNormal='versions/cards/normal.png';versionHover='versions/cards/hover.png';versionSelected='versions/cards/selected.png';versionButtonNormal='versions/buttons/normal.png';versionButtonHover='versions/buttons/hover.png';versionButtonPressed='versions/buttons/pressed.png'}}else{@{}}
 $manifest=[ordered]@{name=$name;author='OpenGDLauncher';version='4.0';backgroundColor=$base;panelColor=$panel;buttonColor=$accent;textColor='#FFFFFF';mutedTextColor='#B8C6D9';ribbonColor=if($name -eq 'Default'){'#2B1553'}else{$accent};ribbonTextColor='#FFFFFF';versionBackgroundColor=$base;versionCardColor=$panel;versionSelectedColor=$accent;fontFamily='LemonMilk';panelOpacity=1;backgroundImage='background.png';sidebarImage='sidebar.png';headerImage='header.png';panelImage='panel.png';cardImage='card.png';logoImage='logo.png';iconImage='icon.png';decorationImage='decoration.png';heroImage='hero.png';heroSound='hero.wav';heroSoundEnabled=$true;ribbonImage=$extraImage.ribbon;steamCardImage=$extraImage.steam;contentCardImage=$extraImage.content;versionBackgroundImage=$extraImage.versionBackground;versionCardNormalImage=$extraImage.versionNormal;versionCardHoverImage=$extraImage.versionHover;versionCardSelectedImage=$extraImage.versionSelected;versionButtonNormalImage=$extraImage.versionButtonNormal;versionButtonHoverImage=$extraImage.versionButtonHover;versionButtonPressedImage=$extraImage.versionButtonPressed;buttonNormalImage='buttons/normal.png';buttonHoverImage='buttons/hover.png';buttonPressedImage='buttons/pressed.png';buttonDisabledImage='buttons/disabled.png';navigationNormalImage='navigation/normal.png';navigationHoverImage='navigation/hover.png';navigationPressedImage='navigation/pressed.png';navigationDisabledImage='navigation/disabled.png'}
 $manifest['modRowImage']=if($name -eq 'GraphicDemo'){'mod-row.png'}else{$null}
 $manifest['steamIndicatorColor']=if($name -eq 'Default'){'#43D79B'}else{$accent}
 $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $folder 'theme.json') -Encoding utf8
}
Theme (Join-Path $project 'ThemeAssets/Default') 'Default' '#0B0D18' '#141628' '#7464F5' '#8B7BFF' '#5140C8'
Theme (Join-Path $PSScriptRoot 'GraphicDemo') 'GraphicDemo' '#120F26' '#30224F' '#EF6B42' '#FF986D' '#BA4D32'
Remove-Item -LiteralPath (Join-Path $PSScriptRoot 'GraphicDemo.zip') -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $PSScriptRoot 'GraphicDemo/*') -DestinationPath (Join-Path $PSScriptRoot 'GraphicDemo.zip')
