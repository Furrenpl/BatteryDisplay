param()
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
function Check-Exit { if ($LASTEXITCODE -ne 0) { throw "Build command failed: $LASTEXITCODE" } }
$toolDir = Join-Path $PSScriptRoot '.build\tools'
$publishDir = Join-Path $PSScriptRoot '.build\publish'
$wix = Join-Path $toolDir 'wix.exe'
if (-not (Test-Path -LiteralPath $wix)) {
    dotnet tool install wix --tool-path $toolDir --version 5.0.2
    Check-Exit
}
& $wix extension add -g WixToolset.UI.wixext/5.0.2
Check-Exit
dotnet publish BatteryDisplay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $publishDir
Check-Exit
New-Item -ItemType Directory -Force releases | Out-Null
& $wix build installer\Package.wxs -arch x64 -ext WixToolset.UI.wixext/5.0.2 -culture ru-RU -d "PublishDir=$publishDir" -d "SourceDir=$PSScriptRoot\installer" -out releases\BatteryDisplay-1.0.0-x64.msi -pdbtype none
Check-Exit
Get-FileHash releases\BatteryDisplay-1.0.0-x64.msi -Algorithm SHA256 | Format-List
