# Creates DocumentManagementSystem on PostgreSQL and SQL Server (idempotent).
# Run from repo root: .\backend\scripts\create-databases.ps1

$ErrorActionPreference = "Stop"
$dbName = "DocumentManagementSystem"

Write-Host "=== PostgreSQL ===" -ForegroundColor Cyan
$psql = Get-ChildItem "C:\Program Files\PostgreSQL\*\bin\psql.exe" -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
if (-not $psql) {
    Write-Warning "psql not found. Install PostgreSQL or add psql to PATH."
} else {
    $pgPassword = $env:PGPASSWORD
    if (-not $pgPassword) {
        $pgPassword = Read-Host "PostgreSQL password for user 'postgres'" -AsSecureString
        $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [Runtime.InteropServices.Marshal]::SecureStringToBSTR($pgPassword))
    }

    $exists = & $psql -h localhost -p 5432 -U postgres -d postgres -tAc `
        "SELECT 1 FROM pg_database WHERE datname = '$dbName';"
  if ($exists -eq "1") {
        Write-Host "PostgreSQL database '$dbName' already exists." -ForegroundColor Green
    } else {
        & $psql -h localhost -p 5432 -U postgres -d postgres -c "CREATE DATABASE `"$dbName`";"
        Write-Host "PostgreSQL database '$dbName' created." -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "=== SQL Server ===" -ForegroundColor Cyan
$sqlcmd = Get-ChildItem "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\*\Tools\Binn\sqlcmd.exe" -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
if (-not $sqlcmd) {
    Write-Warning "sqlcmd not found. Install SQL Server client tools."
} else {
    $instance = if ($env:WDAS_SQL_INSTANCE) { $env:WDAS_SQL_INSTANCE } else { ".\SQLEXPRESS" }
    & $sqlcmd -S $instance -E -C -Q @"
IF DB_ID('$dbName') IS NULL
BEGIN
    CREATE DATABASE [$dbName];
    PRINT 'CREATED';
END
ELSE
    PRINT 'ALREADY_EXISTS';
"@
    Write-Host "SQL Server database '$dbName' on $instance is ready." -ForegroundColor Green
}

Write-Host ""
Write-Host "Switch provider in appsettings.json:" -ForegroundColor Yellow
Write-Host '  "Database": { "Provider": "PostgreSql" }  — or —  "SqlServer"'
