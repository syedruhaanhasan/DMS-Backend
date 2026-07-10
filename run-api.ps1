# Redirect temp/NuGet scratch to D: when C: is low on space.
$env:TEMP = "D:\dotnet-temp"
$env:TMP = "D:\dotnet-temp"
New-Item -ItemType Directory -Force -Path $env:TEMP, "D:\nuget-packages" | Out-Null

dotnet run --project "$PSScriptRoot\src\WDAS.Api"
