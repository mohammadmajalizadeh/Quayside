# Contributing to Quayside

## Clean-room rule (non-negotiable)

Quayside is an independent reimplementation written from a functional specification
(`docs/SPEC.md`). Contributions must be your own work.

- Do **not** submit code, SQL, assets, icons, report layouts or text that was copied,
  translated or decompiled from any other shipping-agency product.
- Do **not** commit third-party binaries, customer workbooks or real operational data.
  Test fixtures must be synthetic (generate them in test code).
- Behaviour questions are answered by `docs/SPEC.md`; if the spec is silent, open an issue
  and add an entry to its "Open questions" section instead of guessing.

## Development

Requirements: .NET SDK 8.0 (see `global.json`). The WPF app builds on any OS
(`EnableWindowsTargeting`) but runs on Windows only. Everything else builds and tests
on Windows, Linux and macOS.

```
dotnet build Quayside.sln
dotnet test  Quayside.sln
```

PostgreSQL migration tests run when `QUAYSIDE_TEST_PG` holds a connection string to a
disposable database user with CREATEDB rights; otherwise they are skipped.

## Pull requests

- One logical change per PR; Conventional Commit messages (`feat:`, `fix:`, `test:` ...).
- Every business rule (`BR-*` in SPEC.md) needs a unit test that names the rule.
- Schema changes: add a migration for **both** providers (see README "Migrations").
- `dotnet build` must be warning-free (warnings are errors).
