$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'SnmpVideoWall\SnmpVideoWall.csproj'
$output = Join-Path $PSScriptRoot 'publish\win-x64'
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $output
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
Write-Host "Publish complete: $output"
Write-Host 'Copy the entire folder to the other PC and keep monitors.json next to SnmpVideoWall.exe.'
