# Monorepo TODO

Planned tools and work items across the Axis Solutions monorepo.

## Planned Tools

### Promun: Database Schema Extractor

**Location:** `sdk/promun/tools/Axis.Promun.SchemaExport/`

Extract the full database schema from all six Promun OpenEdge databases and produce an organised folder structure plus DBML output for documentation and analysis.

**Databases:** rec, inc, promun, exd, hrs, arc

**Output structure:**
```
output/
  rec/
    {table}.df
  inc/
    {table}.df
  promun/
    {table}.df
  ...
  schema.dbml          # combined DBML for all databases
  rec.dbml             # per-database DBML
  inc.dbml
  ...
```

**Requirements:**
- Connect via 32-bit ODBC (same as existing SDK tools)
- Extract table definitions in OpenEdge `.df` format
- Convert `.df` definitions to DBML (dbdiagram.io format)
- Handle indexes, sequences, and field descriptions
- Support extracting a single database or all six

---

### Promun: Program Preprocessor

**Location:** `sdk/promun/tools/Axis.Promun.Preprocess/`

Resolve and flatten a Progress/OpenEdge program source file into a single preprocessed output. Recursively resolves all `{include.i}` directives. Also discovers other programs referenced (via `RUN` statements, etc.) and preprocesses those too.

**Requirements:**
- Accept a program path (e.g., `mun060.p`) and a PROPATH search list
- Recursively resolve all `{include.i}` includes into a single flat file
- Handle preprocessor arguments (`{1}`, `{2}`, `{&arg}`)
- Scan for `RUN program.p` / `RUN program.r` references
- Automatically preprocess discovered programs into the same output folder
- Output: `output/{program}.preprocessed.p` for each program
- Report include dependency tree

---

### FDMS: Device Status Checker

**Location:** `sdk/fdms/tools/Axis.FDMS.DeviceStatus/`

Query FDMS (ZIMRA Fiscal Device Management System) API to check the current status of registered fiscal devices.

**Requirements:**
- Accept device ID and API credentials (or read from config)
- Query device registration status, fiscal day status (open/closed), last submitted invoice
- Display device certificate expiry dates
- Support checking multiple devices in batch
- Output: console summary + optional JSON export

---

### FDMS: Device Activation & Certificate Tool

**Location:** `sdk/fdms/tools/Axis.FDMS.DeviceActivation/`

Activate new FDMS fiscal devices and retrieve/install their device certificates.

**Requirements:**
- Register a new device with ZIMRA FDMS API
- Retrieve the device certificate after activation
- Install certificate to the local certificate store (CurrentUser or LocalMachine)
- Display certificate thumbprint for configuration
- Support certificate renewal for expiring devices
- Output: activation confirmation + certificate details for `appsettings.json` configuration

---

## Completed Restructuring

- [x] Move Promun gRPC service from `sdk/promun/src/services/` to `products/promun-grpc/` (2026-04-04)
- [x] Move GrpcService.Client and Protos up to `sdk/promun/src/` (2026-04-04)
- [x] Rename `products/estatements/Statements.AxisConnect/` to `products/promun-estatements/` (2026-04-04)
