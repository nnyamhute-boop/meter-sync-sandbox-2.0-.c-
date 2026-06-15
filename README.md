# Meter Sync Sandbox

This repository contains a small C# solution that implements a realtime meter-reading sync pipeline:

- MeterSync.Api: minimal ASP.NET Core API that receives realtime readings via POST /api/v1/readings
- MeterSync.Core: core models, normalizer, and orchestrator
- MeterSync.Writers: Postgres writer (uses Npgsql) with a dry-run fallback when no connection string is provided

Requirements
- .NET 7 SDK
- (Optional) Postgres if you want to actually write data

Build and run

dotnet build
dotnet run --project src/MeterSync.Api

Environment
- METER_SYNC_PG: optional Postgres connection string. If not set the writer will log SQL instead of committing.

Example curl

curl -X POST http://localhost:5000/api/v1/readings -H "Content-Type: application/json" -d \
'{"meterId":"meter-123","timestamp":"2026-06-12T12:00:00Z","value":42.5,"source":"test"}'

Notes
- The writer expects a table `meter_readings (meter_id text, timestamp timestamptz, value numeric, source text, quality text)` in the target database.
