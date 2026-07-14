# BlunderForge ♟️

> Your local chess sparring partner. Tough when you want it, helpful when you need it.

[![CI](https://github.com/janouwehand/blunderforge/actions/workflows/ci.yml/badge.svg)](https://github.com/janouwehand/blunderforge/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/janouwehand/blunderforge)](https://github.com/janouwehand/blunderforge/releases/latest)

BlunderForge is a private, single-player chess app that runs on your own machine.
Play against Stockfish, ask for a nudge when you are stuck, revisit finished
games, and export them as PGN.

No account. No rating grind. No mysterious opponent behind the curtain.

## Pick a fight

- 🎯 Choose an approximate opponent Elo from **200 to 3000**.
- ⚪ Play White, Black, or let BlunderForge surprise you.
- 💡 Press **Coach me** for a Stockfish suggestion, highlights, and an arrow.
- ↩️ Take back your latest turn when curiosity wins.
- 📚 Resume unfinished games and replay completed ones.
- 📄 Export any finished game as PGN.
- 🤖 Add optional AI explanations and reviews with DeepSeek or an
  OpenAI-compatible provider.

BlunderForge plays one standard-chess game at a time. Every accepted move is
saved immediately, so closing the browser or restarting the container does not
cost you the position.

## Start playing

The ready-to-run image contains the web app, React frontend, and Stockfish 17.1.
It supports Linux `amd64` and `arm64`.

```powershell
docker volume create blunderforge-data

docker run -d `
  --name blunderforge `
  --restart unless-stopped `
  -p 8085:8080 `
  -v blunderforge-data:/app/data `
  ghcr.io/janouwehand/blunderforge:0.1
```

Open <http://localhost:8085> and make your first move.

The named volume keeps your games and settings safe when the container is
replaced. AI is not required. To add a DeepSeek or OpenAI-compatible key, follow
the copy-ready instructions in the
[latest release](https://github.com/janouwehand/blunderforge/releases/latest#configure-an-ai-api-key-optional).

## Chess truth comes first

Stockfish and deterministic chess rules decide what is legal and what is good.
AI may explain a move in friendlier language, but it never replaces the engine.
If an AI request fails, BlunderForge falls back to deterministic content.

Lower Elo levels use calibrated randomness across a broad set of Stockfish
candidates. From Elo 1320 upward, BlunderForge uses Stockfish's native
`UCI_LimitStrength` and `UCI_Elo` options. These are practical approximations,
not official ratings.

BlunderForge deliberately does **not** build a learning profile, estimate your
rating, or adapt across games.

## Optional AI coach

The app works fully without a provider key. When AI is enabled, you can request:

- a concise explanation alongside the Stockfish coaching move;
- an end-of-game review;
- a review later from game history if you skipped it initially.

The AI Coach screen stores only non-secret settings such as provider, URL,
models, timeout, and retries. API keys come from container environment variables
or secret files and are never shown in the UI.

See the
[v0.1.0 release guide](https://github.com/janouwehand/blunderforge/releases/tag/v0.1.0#configure-an-ai-api-key-optional)
for complete DeepSeek and OpenAI-compatible examples.

## Your data stays yours

BlunderForge stores one SQLite database at
`/app/data/blunderforge.db`. In the command above, that directory is backed by
the `blunderforge-data` Docker volume.

```powershell
docker inspect --format='{{.State.Health.Status}}' blunderforge
docker logs blunderforge
```

Stopping or removing the container leaves the volume intact. Do not delete the
volume unless you intentionally want to delete every game and setting.

## Build and tinker

You need **.NET 10** and **Node.js 24**.

<details>
<summary><strong>Backend</strong></summary>

```bash
dotnet restore BlunderForge.sln
dotnet build BlunderForge.sln --no-restore
dotnet test BlunderForge.sln --no-build
dotnet run --project src/BlunderForge.Web
```

</details>

<details>
<summary><strong>Frontend</strong></summary>

```bash
cd src/BlunderForge.Web/ClientApp
npm ci
npm run lint
npm run typecheck
npm run test
npm run build
```

Use `npm run dev` for the Vite development server.

</details>

<details>
<summary><strong>Container development</strong></summary>

```bash
docker compose -f docker-compose.dev.yml up --build
```

Useful image checks:

```powershell
./scripts/docker-stockfish-smoke.ps1
docker build --target stockfish-integration-tests .
```

</details>

Contributions are welcome. Start with [CONTRIBUTING.md](CONTRIBUTING.md).

## Useful doors

| Route | What lives there |
| --- | --- |
| `/play` | Your active game |
| `/games` | History, replay, reviews, and PGN export |
| `/ai-coach` | Non-secret AI provider settings |
| `/health` | Application liveness |
| `/ready` | Database and Stockfish readiness |
| `/health/ai` | Optional AI provider status |

Maintained API examples live in
[`BlunderForge.Web.http`](src/BlunderForge.Web/BlunderForge.Web.http).

## Built to ship

Every push and pull request runs backend tests, frontend checks, dependency
audits, real-Stockfish integration tests, and a production container build.

Semantic version tags publish multi-architecture images to
[`ghcr.io/janouwehand/blunderforge`](https://github.com/users/janouwehand/packages/container/package/blunderforge)
with an SBOM and GitHub build-provenance attestations.

See [Releases](https://github.com/janouwehand/blunderforge/releases) for
version-specific setup notes and immutable image digests.

## Coming from ChessLearner?

BlunderForge replaces the old adaptive-trainer database schema. It cannot open
or migrate a ChessLearner database.

Back up anything you want to keep, stop the old application, and start
BlunderForge with a new database or Docker volume. Never point BlunderForge at
the old SQLite file. Startup will not silently delete or recreate an existing
database.

## License

BlunderForge is available under the [MIT license](LICENSE).

The container includes Stockfish 17.1 as a separate GPLv3-or-later executable.
Its exact revision and redistribution details are documented in
[`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md). The image also carries the
Stockfish license and complete corresponding source under
`/usr/share/stockfish/`.
