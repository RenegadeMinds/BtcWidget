<#
.SYNOPSIS
  Builds the two release artifacts into .\release\:
    BtcWidget-<version>-win-x64.zip   framework-dependent (small; needs the .NET 10 Desktop Runtime)
    BtcWidget-<version>-win-x64.exe   self-contained single file (bigger; needs nothing)
  Run from the repository root:  .\build-release.ps1
#>
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$proj = "BtcWidget\BtcWidget.csproj"
[xml]$csproj = Get-Content $proj
$version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> in $proj" }
$out = Join-Path $PSScriptRoot "release"
New-Item -ItemType Directory -Force $out | Out-Null
Remove-Item (Join-Path $out "BtcWidget-$version-win-x64.*") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $PSScriptRoot "publish") -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "== framework-dependent zip ==" -ForegroundColor Cyan
dotnet publish $proj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -o publish\zip -nologo -v q
if ($LASTEXITCODE) { throw "publish (zip) failed" }
Remove-Item publish\zip\*.pdb, publish\zip\*.xml -ErrorAction SilentlyContinue
Compress-Archive -Path publish\zip\* -DestinationPath (Join-Path $out "BtcWidget-$version-win-x64.zip") -CompressionLevel Optimal

Write-Host "== self-contained single exe ==" -ForegroundColor Cyan
dotnet publish $proj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=none -o publish\exe -nologo -v q
if ($LASTEXITCODE) { throw "publish (exe) failed" }
Copy-Item publish\exe\BtcWidget.exe (Join-Path $out "BtcWidget-$version-win-x64.exe")

Get-ChildItem $out | Select-Object Name, @{ n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize
