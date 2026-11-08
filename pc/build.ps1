# Compile ScreenLinkPC en un seul exe (compilateur C# fourni avec Windows, .NET Framework 4.x) : .\build.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out  = Join-Path $root '..\dist'
New-Item -ItemType Directory -Force $out | Out-Null
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'


$ico = Join-Path $root 'ScreenLink.ico'
$src = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
& $csc /nologo /target:winexe /platform:x64 /unsafe /optimize+ /codepage:65001 "/out:$out\ScreenLinkPC.exe" "/win32icon:$ico" `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll $src
if ($LASTEXITCODE -ne 0) { throw 'Echec de la compilation' }
"OK : dist\ScreenLinkPC.exe"
