<#
.SYNOPSIS
    Backup database PostgreSQL (Supabase) ra file .dump (custom format).

.DESCRIPTION
    Yêu cầu pg_dump (PostgreSQL client tools) có trong PATH.
    Connection string đọc từ biến môi trường NAMVIET_DB_URL (KHÔNG hardcode vào file).

    Định dạng NAMVIET_DB_URL:
      postgresql://<user>:<password>@<host>:5432/postgres

.EXAMPLE
    $env:NAMVIET_DB_URL = "postgresql://user:pass@host:5432/postgres"
    .\scripts\backup-db.ps1 -OutDir "D:\Backups\NamVietERP"
#>
param(
    [string]$OutDir = (Join-Path $PSScriptRoot "..\backups"),
    [int]$KeepDays = 30
)

$ErrorActionPreference = "Stop"

if (-not $env:NAMVIET_DB_URL) {
    Write-Error "Thiếu biến môi trường NAMVIET_DB_URL. Xem hướng dẫn trong docs/BACKUP.md"
    exit 1
}

if (-not (Get-Command pg_dump -ErrorAction SilentlyContinue)) {
    Write-Error "Không tìm thấy pg_dump. Cài PostgreSQL client tools: https://www.postgresql.org/download/"
    exit 1
}

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outFile = Join-Path $OutDir "namviet-erp-$stamp.dump"

Write-Host "Đang backup database -> $outFile"
pg_dump --format=custom --no-owner --no-privileges --file=$outFile $env:NAMVIET_DB_URL

if ($LASTEXITCODE -ne 0) {
    Write-Error "pg_dump thất bại (exit $LASTEXITCODE)"
    exit $LASTEXITCODE
}

$size = [math]::Round((Get-Item $outFile).Length / 1MB, 2)
Write-Host "Backup xong: $outFile ($size MB)"

# Xóa backup cũ hơn $KeepDays ngày
Get-ChildItem -Path $OutDir -Filter "namviet-erp-*.dump" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepDays) } |
    ForEach-Object {
        Write-Host "Xóa backup cũ: $($_.Name)"
        Remove-Item $_.FullName -Force
    }
