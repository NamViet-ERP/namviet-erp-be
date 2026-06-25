# Seed material units into PostgreSQL (idempotent).
# Usage:
#   .\scripts\seed-material-units.ps1
#   .\scripts\seed-material-units.ps1 -Fresh   # truncate + re-insert

param(
    [string]$Container = "namviet-erp-postgres",
    [string]$DbUser = "erp_admin",
    [string]$DbName = "namviet_erp",
    [switch]$Fresh
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sqlFile = Join-Path $scriptDir "seed-material-units.sql"

function Invoke-ContainerSqlFile {
    param([string]$LocalPath)

    $fileName = Split-Path $LocalPath -Leaf
    $containerPath = "/tmp/$fileName"

    docker cp $LocalPath "${Container}:${containerPath}"
    try {
        docker exec $Container psql -U $DbUser -d $DbName -v ON_ERROR_STOP=1 -f $containerPath
        if ($LASTEXITCODE -ne 0) {
            throw "SQL failed: $fileName"
        }
    }
    finally {
        docker exec $Container rm -f $containerPath | Out-Null
    }
}

if ($Fresh) {
    Write-Host "Truncating warehouse.material_units ..."
    docker exec $Container psql -U $DbUser -d $DbName -v ON_ERROR_STOP=1 -c `
        'TRUNCATE TABLE warehouse.material_units RESTART IDENTITY CASCADE;'
    if ($LASTEXITCODE -ne 0) {
        throw "Truncate failed."
    }
}

Write-Host "Seeding material units into $DbName ..."
Invoke-ContainerSqlFile -LocalPath $sqlFile
Write-Host "Done."
