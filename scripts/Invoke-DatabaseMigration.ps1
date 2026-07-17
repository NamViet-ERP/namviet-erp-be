[CmdletBinding()]
param(
    [ValidateSet("migrate", "script")]
    [string]$Command = "migrate",

    [string]$OutputPath = "artifacts/database-migrations.sql"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $PSScriptRoot "DatabaseMigrator/DatabaseMigrator.csproj"

Push-Location $repoRoot
try {
    if ($Command -eq "script") {
        dotnet run --project $project --configuration Release -- script --output $OutputPath
    }
    else {
        dotnet run --project $project --configuration Release -- migrate
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Database migrator exited with code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
