$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
Add-Type -Path (Join-Path $project 'Launcher.Themes/bin/Release/net8.0-windows/Launcher.Themes.dll')
Add-Type -Path (Join-Path $project 'Launcher.Workshop/bin/Release/net8.0-windows/Launcher.Workshop.dll')
$root=Join-Path $PSScriptRoot '_verification'
$full=[IO.Path]::GetFullPath($root)
$expected=[IO.Path]::GetFullPath($PSScriptRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
if(-not $full.StartsWith($expected,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe verification path'}
if(Test-Path -LiteralPath $full){Remove-Item -LiteralPath $full -Recurse -Force}
New-Item -ItemType Directory -Path $full | Out-Null
try {
 Copy-Item -LiteralPath (Join-Path $project 'ThemeAssets/Default') -Destination (Join-Path $full 'Default') -Recurse
 $service=[Launcher.Themes.ThemeService]::new($full)
 $workshop=[Launcher.Workshop.LocalWorkshop]::new($service)
 $workshop.Import((Join-Path $project 'ThemeKit/GraphicDemo.zip'))
 $standard=$service.Load('Default');$graphic=$service.Load('GraphicDemo')
 foreach($name in @('BackgroundImage','SidebarImage','HeaderImage','PanelImage','CardImage','LogoImage','IconImage','DecorationImage','HeroImage','RibbonImage','SteamCardImage','ContentCardImage','ModRowImage','VersionBackgroundImage','VersionCardNormalImage','VersionCardHoverImage','VersionCardSelectedImage','VersionButtonNormalImage','VersionButtonHoverImage','VersionButtonPressedImage','ButtonNormalImage','ButtonHoverImage','ButtonPressedImage','ButtonDisabledImage','NavigationNormalImage','NavigationHoverImage','NavigationPressedImage','NavigationDisabledImage')){
  if(-not $service.ImagePath('GraphicDemo',$graphic.$name)){throw "Missing asset: $name"}
 }
 if(-not $service.ResolveSound('GraphicDemo',$graphic.HeroSound,$standard.HeroSound)){throw 'Missing hero sound'}
 $graphicBackground=$service.ResolveImage('GraphicDemo',$graphic.BackgroundImage,$standard.BackgroundImage)
 $defaultBackground=$service.ResolveImage('Default',$standard.BackgroundImage,$standard.BackgroundImage)
 if((Get-FileHash $graphicBackground).Hash -eq (Get-FileHash $defaultBackground).Hash){throw 'Theme switching did not change graphics'}
 $fallback=$service.ResolveImage('GraphicDemo',$null,$standard.SidebarImage)
 if($fallback -ne $service.ImagePath('Default',$standard.SidebarImage)){throw 'Fallback failed'}
 $service.Delete('GraphicDemo')
 if($service.Names() -contains 'GraphicDemo'){throw 'Theme deletion failed'}
 Write-Output 'PASS: ZIP installation, 28 PNG assets, WAV, graphic switching, fallback, deletion'
} finally {if(Test-Path -LiteralPath $full){Remove-Item -LiteralPath $full -Recurse -Force}}
