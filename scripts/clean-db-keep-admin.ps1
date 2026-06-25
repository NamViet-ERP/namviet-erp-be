# Clean NamViet ERP DB - keep only seed admin account.
# Usage: .\scripts\clean-db-keep-admin.ps1 [-AdminEmail "admin@namviet.vip"]

param(
    [string]$AdminEmail = "admin@namviet.vip",
    [string]$Container = "namviet-erp-postgres",
    [string]$DbUser = "erp_admin",
    [string]$DbName = "namviet_erp"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sqlFile = Join-Path $scriptDir "clean-db-keep-admin.sql"

Write-Host "Cleaning database $DbName - keeping admin: $AdminEmail"
Write-Host "WARNING: This deletes all WMS/HR data and non-admin users."

$fileName = Split-Path $sqlFile -Leaf
$containerPath = "/tmp/$fileName"
docker cp $sqlFile "${Container}:${containerPath}"
try {
    docker exec $Container psql -U $DbUser -d $DbName -v ON_ERROR_STOP=1 -v admin_email="$AdminEmail" -f $containerPath
    if ($LASTEXITCODE -ne 0) {
        throw "Database cleanup failed."
    }
}
finally {
    docker exec $Container rm -f $containerPath | Out-Null
}

Write-Host "Done."
