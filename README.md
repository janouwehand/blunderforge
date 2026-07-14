# BlunderForge

BlunderForge is a local, single-user guided chess application. Play one standard-chess game at a time against Stockfish, choose an approximate opponent Elo from 200 through 3000, ask Stockfish for objective move help, and optionally add a concise AI explanation.

Stockfish and deterministic chess logic are always the source of chess truth. AI is optional: games, Stockfish coaching, history, and PGN export continue to work without a provider. Reviews are requested only with explicit opt-in and an available provider secret; a failed requested review falls back to deterministic content. BlunderForge does not maintain a learning profile, estimate a user rating, or adapt across games.

## Product behavior

- New games default to Random color and opponent Elo 800. White, Black, and any whole Elo from 200 through 3000 are supported.
- Elo 200–1319 uses a calibrated randomized selection from a broad Stockfish candidate set. Elo 1320–3000 uses Stockfish's native `UCI_LimitStrength` and `UCI_Elo` settings. Both regimes are approximate practical strengths.
- Every accepted move is persisted immediately. An active game, including a pending opponent turn, can resume after navigation, browser restart, or container restart.
- `Coach me` is available on the player's turn. It always returns a Stockfish move, text alternative, highlights, and an arrow. When AI is configured, `Use AI explanation and game review` controls both coaching text and automatic end-of-game review requests; clearing it makes neither AI call.
- The latest player turn can be taken back. Active and historical games can be permanently deleted after confirmation.
- Completed and resigned games appear in paginated history, support move replay, and can be exported as PGN. If no review was requested at game end, it can be generated once later through an explicit action when AI and its secret are available.
- Primary screens have direct routes: `/play`, `/games`, `/games/{gameId}`, and `/ai-coach`, including browser back/forward support.

## Local development

The selected toolchains are .NET 10 and Node.js 24. Restore, build, and test the backend:

```bash
dotnet restore BlunderForge.sln
dotnet build BlunderForge.sln --no-restore
dotnet test BlunderForge.sln --no-build
```

Install and verify the frontend:

```bash
cd src/BlunderForge.Web/ClientApp
npm ci
npm run lint
npm run typecheck
npm run test
npm run build
```

Run the backend locally after setting a writable data directory and a valid Stockfish path:

```bash
dotnet run --project src/BlunderForge.Web
```

For frontend-only development, run `npm run dev` in `src/BlunderForge.Web/ClientApp`. The explicit container hot-reload environment is started with:

```bash
docker compose -f docker-compose.dev.yml up --build
```

## Configuration

Non-secret settings use ASP.NET Core configuration keys. The production defaults are:

```env
ConnectionStrings__Default=Data Source=/app/data/blunderforge.db
BlunderForge__DataDirectory=/app/data
BlunderForge__Stockfish__Path=/app/stockfish/stockfish
BlunderForge__AiProvider__Provider=DeepSeek
BlunderForge__AiProvider__BaseUrl=https://api.deepseek.com
BlunderForge__AiProvider__InteractiveModel=deepseek-v4-flash
BlunderForge__AiProvider__ReviewModel=deepseek-v4-pro
BlunderForge__AiProvider__TimeoutSeconds=30
BlunderForge__AiProvider__MaxRetryCount=1
```

The AI Coach screen can store non-secret provider, URL, model, timeout, and retry settings in SQLite. It never displays or accepts an API key. Supported providers are `DeepSeek` and `OpenAICompatible`; interactive and review models are configured separately.

Secrets are read only from environment variables or files. `_FILE` takes precedence when both forms are present:

```env
BLUNDERFORGE_DEEPSEEK_API_KEY=obviously-fake-example
BLUNDERFORGE_DEEPSEEK_API_KEY_FILE=/run/secrets/deepseek_api_key
BLUNDERFORGE_OPENAI_COMPATIBLE_API_KEY=obviously-fake-example
BLUNDERFORGE_OPENAI_COMPATIBLE_API_KEY_FILE=/run/secrets/openai_compatible_api_key
```

For Docker secrets, add a local Compose override that mounts the secret and sets the matching `_FILE` variable. Do not commit the override or secret file. For example:

```yaml
services:
  blunderforge:
    secrets:
      - deepseek_api_key
    environment:
      BLUNDERFORGE_DEEPSEEK_API_KEY_FILE: /run/secrets/deepseek_api_key

secrets:
  deepseek_api_key:
    file: ./secrets/deepseek_api_key
```

Normal page loads check only settings and local secret availability. The explicit `Test connection` action performs the external provider request. No paid AI call is required by the automated test suites.

## API and health endpoints

Maintained request examples are in `src/BlunderForge.Web/BlunderForge.Web.http`.

- `/api/games` and `/api/games/active/*`: start/resume, legal moves, move submission, pending opponent turn, coaching, takeback, resignation, and hard deletion.
- `/api/games/{gameId}`, `/review`, and `/pgn`: historical detail, explicit one-time review generation/retrieval, and PGN export.
- `/api/settings/ai-provider`: read or update non-secret settings; `/test` explicitly tests connectivity.
- `/health` and `/health/live`: application liveness.
- `/ready`: database migration and Stockfish readiness.
- `/health/ai`: optional AI provider status; AI unavailability does not fail general readiness.

API write bodies are limited to 64 KiB. Errors contain a safe correlation ID, and provider text is rendered as plain text rather than HTML.

## Production container

Build and start the one-container production stack:

```bash
docker compose config
docker compose build
docker compose up -d
```

Compose publishes BlunderForge at `http://localhost:8087` by default; set `BLUNDERFORGE_PORT` to choose another host port. The image contains the ASP.NET Core application, built React assets, and Stockfish 17.1. It runs as the non-root `blunderforge` user and stores the only persistent user database at `/app/data/blunderforge.db` in the `blunderforge-data` volume. The image contains no seeded database or secret. Stop without deleting data using `docker compose down`.

The Dockerfile supports Linux `amd64` and `arm64` Stockfish builds. Run `./scripts/docker-stockfish-smoke.ps1` for a direct UCI image check or `docker build --target stockfish-integration-tests .` for the real-engine integration target.

## Continuous integration and releases

Every pull request and push to `main` runs warning-free .NET restore/build/tests,
frontend lint/type-check/tests/build, NuGet and npm vulnerability audits, the
real-Stockfish container integration target, and a production-image build.
Dependabot checks NuGet, npm, Docker base images, and GitHub Actions weekly.

Pushing a semantic version tag such as `v1.0.0` publishes signed-provenance,
SBOM-enabled Linux `amd64` and `arm64` images to
`ghcr.io/<owner>/<repository>`. Version tags, a major/minor tag, `latest`, and an
immutable commit-SHA tag are generated automatically. The first published GHCR
package should be checked once in GitHub's package settings to confirm that its
visibility is Public and that it inherits access from this repository.

## License

BlunderForge's own source code is available under the [MIT license](LICENSE).
The container includes Stockfish 17.1 as a separate GPLv3-or-later executable.
Its exact revision, corresponding-source link, and redistribution details are in
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). The image itself includes both
the Stockfish license and the complete corresponding source under
`/usr/share/stockfish/`.

## One-time destructive upgrade from ChessLearner

The BlunderForge redesign intentionally replaces the old adaptive-trainer schema with one new initial migration. It does not migrate an old ChessLearner database.

1. Stop the old application: `docker compose down`.
2. If the old app used a bind-mounted SQLite file, back it up if needed and then explicitly remove that old database file. Never point BlunderForge at it.
3. If the old app used the repository's old Docker volume and no data must be retained, identify it with `docker volume ls`, verify its exact name, and remove that specific old volume with `docker volume rm <verified-old-volume-name>`. Do not use a broad prune command.
4. Build the final image with `docker compose build`.
5. Start BlunderForge with `docker compose up -d`. A new empty `blunderforge-data` volume is created and the single initial migration runs automatically.
6. Verify `http://localhost:8087/health` and `http://localhost:8087/ready`, start a game, restart the service with `docker compose restart blunderforge`, and confirm that the active game resumes.

This reset is required once for the redesign. Startup never automatically deletes or recreates an existing database.

## Release verification

Before publishing, run the backend, frontend, dependency, and Docker commands in
`plans/release-hardening.md`. Browser end-to-end tests are intentionally outside
the scope of this small local application; component and API integration tests
cover the supported flows.
