# Seed material category tree into PostgreSQL (no runtime BE seed).
# Usage:
#   .\scripts\seed-material-categories.ps1              # idempotent insert (UTF-8 safe)
#   .\scripts\seed-material-categories.ps1 -Fresh       # truncate + re-insert (fix bad encoding)
#   .\scripts\seed-material-categories.ps1 -RepairNames # UPDATE names only (keeps IDs)
# Regenerate SQL: node scripts/generate-material-categories-sql.mjs

param(
    [string]$Container = "namviet-erp-postgres",
    [string]$DbUser = "erp_admin",
    [string]$DbName = "namviet_erp",
    [switch]$Fresh,
    [switch]$RepairNames
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sqlFile = Join-Path $scriptDir "seed-material-categories.sql"
$repairFile = Join-Path $scriptDir "repair-material-categories-names.sql"

if (-not (Test-Path $sqlFile)) {
    Write-Host "Generating SQL files ..."
    node (Join-Path $scriptDir "generate-material-categories-sql.mjs")
}

function Invoke-ContainerSqlFile {
    param([string]$LocalPath)

    $fileName = Split-Path $LocalPath -Leaf
    $containerPath = "/tmp/$fileName"

    # docker cp preserves UTF-8 — avoid PowerShell pipe encoding issues on Windows
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

if ($RepairNames) {
    if (-not (Test-Path $repairFile)) {
        node (Join-Path $scriptDir "generate-material-categories-sql.mjs")
    }
    Write-Host "Repairing category names in $DbName ..."
    Invoke-ContainerSqlFile -LocalPath $repairFile
    Write-Host "Done."
    return
}

if ($Fresh) {
    Write-Host "Truncating warehouse.material_categories ..."
    docker exec $Container psql -U $DbUser -d $DbName -v ON_ERROR_STOP=1 -c `
        'TRUNCATE TABLE warehouse.material_categories RESTART IDENTITY CASCADE;'
    if ($LASTEXITCODE -ne 0) {
        throw "Truncate failed."
    }
}

Write-Host "Seeding material categories into $DbName ..."
Invoke-ContainerSqlFile -LocalPath $sqlFile
Write-Host "Done."
