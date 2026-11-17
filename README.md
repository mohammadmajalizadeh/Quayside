# Quayside

Open-source Windows desktop system for **container shipping agencies and freight forwarders**:
bills of lading, the container lifecycle (release to shipper → full return; discharge → empty
return), configurable demurrage/detention, invoicing and payments, and Excel interchange with
existing container workbooks.

.NET 8 · WPF · EF Core · SQLite (default) or PostgreSQL · ClosedXML · MIT licensed.

> **Independent reimplementation.** Quayside is written from scratch from a functional
> specification ([`docs/SPEC.md`](docs/SPEC.md)). It is not affiliated with, endorsed by, or
> derived from the source code of the vendor of any legacy system whose workflows or file
> formats it supports. No code, assets or branding from such systems are included.

## Features (v0.1)

| Area | What works | Spec |
|---|---|---|
| Containers | ISO 6346 validation (check digit configurable as warning/error), equipment master, line owner-prefix rules, cargo type ST/DG/OG checks | BR-C1..C8 |
| Lifecycle | Release to shipper, full return, discharge, empty return with depot and body state; ordering, prerequisite and lock rules | BR-L1..L6 |
| Demurrage / detention | Per-tariff free days; contiguous day-range slabs; **separate daily rate per equipment class (20, 40, 40HC, 45, Reefer)**; configurable day counting (default: day of discharge counts, both ends inclusive); optional rate fallbacks | BR-D1..D6 |
| Finance | Draft (negative) and per-type final numbering, VAT and per-currency rounding, credit/debit notes, demurrage invoices, payment allocation, statement of account | BR-I1..I6 |
| Excel | Import **and** export of six workbook layouts (container master, B/L container list, RTS, F.RTN, F.RTN + attach to B/L, MT RTN) with preview, per-row results and an explicit backfill mode | [`docs/EXCEL_FORMATS.md`](docs/EXCEL_FORMATS.md) |
| Settings | Every behaviour that rests on an unconfirmed assumption is a setting, flagged **⚠ ASSUMPTION** in the UI | |
| Database | SQLite file by default; PostgreSQL by configuration; migrations for both, tested in CI | |

Not yet implemented (see SPEC §10): CRM/job approval workflow, reports/printing, EDIFACT,
users and permissions, Jalali date display (setting exists, UI pending).

## Screenshots

_None yet._ The WPF UI compiles in CI but has not yet been run and captured on Windows, so
there are no screenshots. Contributions welcome. Add PNGs under `docs/screenshots/`.

## Build and run

Requirements: [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) (pinned by `global.json`).

```powershell
git clone <repo-url> quayside
cd quayside
dotnet build Quayside.sln
dotnet test  Quayside.sln
dotnet run --project src/Quayside.App      # Windows only
```

All projects (including the WPF app, via `EnableWindowsTargeting`) **build** on Linux/macOS, and all
tests run there. The app itself runs on Windows 10/11 x64.

### Database configuration

Default: SQLite at `%LOCALAPPDATA%\Quayside\quayside.db`, created and migrated on first start.

PostgreSQL: create `%LOCALAPPDATA%\Quayside\quayside.json`:

```json
{ "database": { "provider": "Postgres", "connectionString": "Host=db;Database=quayside;Username=quayside;Password=..." } }
```

or set `QUAYSIDE_DB_PROVIDER=Postgres` and `QUAYSIDE_DB_CONNECTION=...`. Keep passwords out of
source control; prefer the per-user config file or a secret store.

### Tests

```bash
dotnet test                                   # SQLite + domain tests; PostgreSQL tests skip
QUAYSIDE_TEST_PG="Host=localhost;Username=postgres;Password=...;Database=postgres" dotnet test
QUAYSIDE_SAMPLES=/path/to/sample/workbooks dotnet test --filter RealSample   # optional, local only
```

`QUAYSIDE_TEST_PG` must allow `CREATE DATABASE`; each test uses and drops its own database.
`QUAYSIDE_REQUIRE_PG=1` turns skipped PostgreSQL tests into failures (used in CI).

### Migrations

Each provider has its own migrations assembly. After changing the model, add a migration to **both**:

```bash
dotnet tool restore
dotnet ef migrations add <Name> -p src/Quayside.Data.Sqlite   -s src/Quayside.Data.Sqlite   -o Migrations
dotnet ef migrations add <Name> -p src/Quayside.Data.Postgres -s src/Quayside.Data.Postgres -o Migrations
```

A test fails if either provider's migrations are out of date with the model.

### Packaging (Windows)

```powershell
./build/package.ps1 -Version 0.1.0
```

Produces `artifacts/Quayside-<ver>-win-x64-portable.zip` (self-contained, no .NET install needed)
and, if [Inno Setup 6](https://jrsoftware.org/isinfo.php) is installed,
`artifacts/Quayside-<ver>-win-x64-setup.exe`. CI builds both on `windows-latest` and uploads
them as workflow artifacts.

## Repository layout

```
src/Quayside.Domain          entities, business rules, demurrage engine, settings (no dependencies)
src/Quayside.Data            EF Core context, provider selection, settings store, finance service
src/Quayside.Data.Sqlite     SQLite migrations
src/Quayside.Data.Postgres   PostgreSQL migrations
src/Quayside.Excel           workbook templates, importer, exporter (ClosedXML)
src/Quayside.App             WPF desktop app (MVVM)
tests/Quayside.Tests         xUnit tests; each business rule is named in its test (BR-*)
installer/, build/           Inno Setup script, packaging script
docs/                        functional spec and Excel format reference
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). The clean-room rule applies to all contributions.

## License

[MIT](LICENSE)
