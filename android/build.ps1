# Compile ScreenLink (Android) : .\build.ps1  -> ..\dist\ScreenLink-<version>.apk
# Utilise la chaîne d'outils portable de PhoneSDRLink (JDK, Gradle, SDK Android).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$tc = 'J:\claude\SDRLINK Smartphone\toolchain'
$env:JAVA_HOME = "$tc\jdk"
Set-Location $root
& "$tc\gradle\bin\gradle.bat" --no-daemon -q assembleRelease
if ($LASTEXITCODE -ne 0) { throw 'Echec de la compilation' }
$ver = ([regex]::Match((Get-Content "$root\app\build.gradle" -Raw), "versionName '([^']+)'")).Groups[1].Value
New-Item -ItemType Directory -Force "$root\..\dist" | Out-Null
$apk = Get-ChildItem "$root\app\build\outputs\apk\release\*.apk" | Select-Object -First 1
Copy-Item $apk.FullName "$root\..\dist\ScreenLink-$ver.apk" -Force
"OK : dist\ScreenLink-$ver.apk"
