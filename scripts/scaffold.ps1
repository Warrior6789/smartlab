# Re-generate SmartLab.DAL/Entities and Context/AppDbContext.cs from the database (database-first).
# Connection string is read from user-secrets of SmartLab.API (ConnectionStrings:DefaultConnection).
# Usage (from repo root or anywhere):  ./scripts/scaffold.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed" }

    dotnet ef dbcontext scaffold "Name=ConnectionStrings:DefaultConnection" Npgsql.EntityFrameworkCore.PostgreSQL `
        -p SmartLab.DAL -s SmartLab.API `
        --output-dir Entities --context-dir Context `
        --context AppDbContext --namespace SmartLab.DAL.Entities --context-namespace SmartLab.DAL.Context `
        --no-onconfiguring --force
    if ($LASTEXITCODE -ne 0) { throw "Scaffold failed" }

    Write-Host "Scaffold completed." -ForegroundColor Green
}
finally {
    Pop-Location
}
