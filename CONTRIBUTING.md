# Contributing

Thanks for helping improve BlunderForge. Keep changes focused on the local,
single-user chess trainer and avoid committing secrets, databases, build output,
or personal Compose overrides.

Before opening a pull request, run:

```bash
dotnet restore BlunderForge.sln
dotnet build BlunderForge.sln --no-restore
dotnet test BlunderForge.sln --no-build
cd src/BlunderForge.Web/ClientApp
npm ci
npm run lint
npm run typecheck
npm run test
npm run build
```

Behavioral changes should include tests at the lowest appropriate layer. Use
fake AI providers and the fake Stockfish executable for deterministic tests;
paid provider calls must never be required. The CI container job separately
executes the real Stockfish integration target.
