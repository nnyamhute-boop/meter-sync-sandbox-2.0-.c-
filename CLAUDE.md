# Public Sector Monorepo

Multi-language monorepo for Axis Solutions' public-sector products. Mostly .NET, with Kotlin/Android and a small amount of JS/gRPC tooling.

## Layout
- `products/` — end-user products
  - `unipay/` (api), `sync/` (Axis Sync Engine — multi-ERP sync host for all Axis apps; Promun + Sage plugins), `promun/` (estatements, grpc, mtume-client, online-payments-legacy), `revcoll/` (platform, api-legacy, web-legacy, android-legacy, database-legacy), `konekt/api`, `tools/`
- `sdk/` — internal SDKs published to AxisNugets/Maven: `fdms` (.NET), `fdms-kt` (Kotlin), `promun` (.NET + gRPC protos), `nedbank` (.NET), `unipay` (.NET)
- `libs/blazor-ui/` — shared Blazor component library (Axiom)
- `infrastructure/` — mail stacks (sogo, stalwart) and ops tooling
- `docs/vault/` — Obsidian vault with cross-cutting design notes

Each product/SDK is self-contained with its own solution, build scripts, and `docs/`. There is **no** monorepo-wide solution.

## Stacks in use

### .NET (majority)
- .NET 10 (`global.json` pins SDK 10.0.100; `Directory.Build.props` sets `net10.0`)
- Nullable + ImplicitUsings on; central package versions via `Directory.Packages.props`
- Build/test from any project dir: `dotnet build`, `dotnet test`
- Internal NuGet feed: **AxisNugets** (Azure DevOps) — auth via per-project `nuget.config`. Packages: `Axis.FDMS`, `Axis.Promun.*`, `Axis.Unipay.*`.

### Kotlin / Android (`products/revcoll/android-legacy`, `sdk/fdms-kt`, `build-logic/`)
- Gradle Kotlin DSL. Build with `./gradlew build`, test with `./gradlew test`. Android emulator/instrumented tests via `./gradlew connectedAndroidTest`.

### gRPC / protos (`sdk/promun`)
- `.proto` files in `Axis.Promun.GrpcService.Protos`. Generated clients for C#, Node, Python live under `sdk/promun/samples/`. Regenerate via the SDK's build scripts before consuming proto changes.

### Frontend bits
- A few `package.json` files (e.g. `products/promun/estatements/src/AxisConnect.Web`, `products/unipay/api/src/UNIPAY.Blazor`) for client-side asset bundling. Run `npm install` / `npm run build` only inside those dirs.

## Working in a project
1. Read the project's `README.md` and `docs/` first — that's where stack-specific details (DB setup, secrets, feature flags) live.
2. Build/test that project's own solution. Don't try to build the whole monorepo at once; it's not a unit.
3. For shared concerns (e.g. an SDK change consumed by a product), build the SDK, push the package to AxisNugets (or use a local feed), then bump the version in the consumer's `Directory.Packages.props`.

## Testing
- **Unit tests** belong in `test/` or `tests/` next to the project. Run with `dotnet test` (or `./gradlew test`). Keep them fast and hermetic — no DBs, no network.
- **Integration tests** are explicitly named (`*.IntegrationTests`) and may hit real dependencies (Promun OpenEdge, SQLite, PostgreSQL, FDMS sandbox). They typically need env vars or a local config. Don't run them in tight inner loops.
- Before claiming a change is done: build, then run the project's tests. If you touched an SDK, also build at least one consuming product. If you touched UI, exercise it in a browser — type-checks aren't feature checks.
- For Promun data questions, prefer the `promun-openedge-verify` skill (live ABL queries) over guessing from code.

## Code style
- C# / Kotlin: match surrounding code. Nullable on, prefer immutability, avoid magic strings (use the `*ConfigurationKeys` / constants pattern already established in each project).
- Don't introduce new patterns when an existing one is in use nearby — consistency beats local taste.
- No comments explaining what code does; only why if non-obvious.

## Plugins / extension points
Where ERP/SDK plugins are used (notably `products/sync`, the Axis Sync Engine), providers are discovered at startup via `IErpProviderModule`. Adding one means a new csproj, an `IErpProviderModule` impl, and an `ErpSettings:ActiveProvider` key — see the project's docs for details.

## Secrets & configuration
- Production secrets are typically DPAPI-encrypted in a `config.json` written by the installer. **Never** commit secrets.
- For local dev, prefer `dotnet user-secrets` or env vars.
- `appsettings.json` is base config; environment overrides via `appsettings.{Environment}.json`.

## Git / shipping
- Trunk is `main`. Feature work happens on branches and PRs in Azure DevOps.
- Skills available for the typical flow: `/ship-feature` (general), `/ado-ship` (Azure DevOps PRs), `/backlog` and `/idea` (work items), `/sync-ado` (vault ↔ ADO sync), `/commit`.
- Don't `--no-verify` past hooks. Investigate failures.
