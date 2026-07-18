@echo off
cd /d "d:\document management\backend\run-api-20260717155131"
set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://0.0.0.0:5110
echo Starting WDAS API on http://localhost:5110 ...
dotnet WDAS.Api.dll --urls http://0.0.0.0:5110
if errorlevel 1 (
  echo.
  echo FAILED. Trying dotnet run from source...
  cd /d "d:\document management\backend\src\WDAS.Api"
  dotnet run --launch-profile http --no-launch-profile false
)
pause
