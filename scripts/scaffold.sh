#!/usr/bin/env bash
# Re-generate SmartLab.DAL/Entities and Context/AppDbContext.cs from the database (database-first).
# Connection string is read from user-secrets of SmartLab.API (ConnectionStrings:DefaultConnection).
# Usage (from repo root or anywhere):  ./scripts/scaffold.sh
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet tool restore

dotnet ef dbcontext scaffold "Name=ConnectionStrings:DefaultConnection" Npgsql.EntityFrameworkCore.PostgreSQL \
  -p SmartLab.DAL -s SmartLab.API \
  --output-dir Entities --context-dir Context \
  --context AppDbContext --namespace SmartLab.DAL.Entities --context-namespace SmartLab.DAL.Context \
  --no-onconfiguring --force

echo "Scaffold completed."
