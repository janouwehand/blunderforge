# Public Release Hardening

Prepare BlunderForge for its first public GitHub commit by fixing consistency and runtime-configuration issues, making release tests trustworthy, adding licensing and GitHub automation, and achieving warning-free verified builds. Browser end-to-end testing and creating the initial commit are intentionally out of scope.

## For Future Agents
As work proceeds: mark checkboxes `- [x]` as items complete; when a phase is done,
set its status to `Complete` and write its **Phase Summary** (what was done, key
decisions, anything needed to continue with zero context); run the phase's
**Verification Plan** and record the result before moving on. When all phases are
done, fill in **Final Recap** and **Deployment Plan**.

## Phase 1: Repository and licensing baseline
Status: Complete

- [x] Ignore the private local Compose override without weakening the portable examples.
- [x] Add an MIT license for BlunderForge and third-party notices for Stockfish.
- [x] Make the production image retain the Stockfish GPL license, exact source revision, and corresponding source code.
- [x] Update public documentation for licensing and release behavior.

### Verification Plan
- `git check-ignore -v docker-compose.local.yml` reports the intended ignore rule.
- Inspect the Docker build stages and notices to confirm the exact Stockfish source and GPL text accompany the binary.
- `docker compose config` succeeds.

### Phase Summary
Added MIT licensing for BlunderForge and GPL/source notices for Stockfish. The
Stockfish build is pinned and verified against commit
`03e27488f3d21d8ff4dbf3065603afa21dbd0ef3`; the production image carries the
binary, GPL text, revision marker, and a `git archive` of the exact source.
`docker-compose.local.yml` and `AGENTS.md` are explicitly ignored. Verified with
`git check-ignore`, source/license inspections, and `docker compose config`.

## Phase 2: Persistence concurrency and idempotency
Status: Complete

- [x] Carry an expected persisted version through active-game saves.
- [x] Reject stale conflicting saves without allowing FEN and move rows to diverge.
- [x] Make duplicate submission of the same move idempotent at the application/API boundary.
- [x] Add deterministic concurrent and duplicate-request tests.

### Verification Plan
- `dotnet test tests/BlunderForge.ApplicationTests/BlunderForge.ApplicationTests.csproj` succeeds.
- `dotnet test tests/BlunderForge.InfrastructureTests/BlunderForge.InfrastructureTests.csproj` succeeds, including stale-write coverage.
- `dotnet test tests/BlunderForge.WebTests/BlunderForge.WebTests.csproj` succeeds, including duplicate API submission coverage.

### Phase Summary
Added an aggregate persistence version to rehydrated games and compare it before
every update, while retaining EF's concurrency token for races during the save.
Stale writes now raise `GameConcurrencyException`; duplicate identical move
submissions return the already persisted result. Added stale-write, concurrent
creation, application idempotency, and API idempotency coverage. Verified all
three phase test projects: 28 application, 28 infrastructure, and 17 web tests
passed.

## Phase 3: Dynamic AI resilience
Status: Complete

- [x] Apply stored timeout and retry settings to each provider operation rather than only at startup.
- [x] Preserve transient-only retry and circuit-breaker behavior and accurate retry logging.
- [x] Add tests proving runtime setting changes affect subsequent requests without restart.

### Verification Plan
- `dotnet test tests/BlunderForge.InfrastructureTests/BlunderForge.InfrastructureTests.csproj --filter AiProvider` succeeds.
- Existing authentication, 429, timeout, malformed-response, and circuit-breaker tests remain green.

### Phase Summary
Replaced startup-bound HTTP resilience handlers with cached Polly pipelines keyed
by the currently stored timeout and retry count. Requests are recreated safely
for retries, transient status/exception filtering and circuit breaking remain in
place, and logged retries reflect actual attempts. Runtime retry and timeout
changes are covered without recreating the provider. All 15 AI-provider tests
passed.

## Phase 4: Trustworthy release tests and warning cleanup
Status: Complete

- [x] Make real Stockfish integration tests explicitly skipped or required instead of falsely passing.
- [x] Fix all current .NET analyzer warnings without suppressing meaningful diagnostics.
- [x] Enforce warning-free production builds.
- [x] Add any regression tests required by warning-related behavioral changes.

### Verification Plan
- `dotnet restore BlunderForge.sln` succeeds.
- `dotnet build BlunderForge.sln --no-restore` succeeds with zero warnings.
- `dotnet test BlunderForge.sln --no-build` succeeds with truthful skip reporting.

### Phase Summary
Real Stockfish checks use a discovery-time conditional fact: local runs report
three explicit skips when no binary exists, while the Docker integration target
sets the required binary path and executes them. Fixed invariant PGN formatting,
source-generated logging, concrete JSON collection types, and marked EF migration
files as generated rather than editing generated migrations. Warnings are errors
for all projects. Restore and build succeeded with zero warnings; the solution
reported 85 passed and 3 explicitly skipped real-engine tests.

## Phase 5: GitHub CI/CD and final release verification
Status: Complete

- [x] Add GitHub Actions CI for backend, frontend, dependency audit, and container build.
- [x] Add tagged GHCR publishing with least-privilege permissions and immutable version tags.
- [x] Add Dependabot configuration for NuGet, npm, Docker, and GitHub Actions.
- [x] Run the complete backend, frontend, dependency, Compose, and available container verification suite.
- [x] Confirm no private paths, secrets, databases, build output, or personal settings are publishable.

### Verification Plan
- Validate workflow and Dependabot YAML structure locally.
- `dotnet restore`, warning-free `dotnet build`, and `dotnet test` succeed.
- `npm ci`, `npm run lint`, `npm run typecheck`, `npm run test`, and `npm run build` succeed.
- `dotnet list BlunderForge.sln package --vulnerable --include-transitive` reports no vulnerable packages.
- `npm audit --audit-level=high` succeeds.
- `docker compose config` and, when Docker is available, `docker compose build` succeed.

### Phase Summary
Added SHA-pinned GitHub Actions for backend/frontend CI, dependency audits,
real-Stockfish container integration, production image builds, and tagged
multi-architecture GHCR publication with SBOM and provenance. Added weekly
Dependabot updates, `global.json`, security and contribution guidance. Verified
workflow syntax with checksum-verified actionlint 1.7.12 and YAML parsing. Release
build/test/audit commands, Compose validation, ignore rules, and publishable-file
scans passed. Docker Desktop was unavailable locally, so `docker compose build`
could not run here; the required CI container job will be the first executable
verification of the image and real Stockfish binary.

## Final Recap
BlunderForge is prepared for a first public commit. It now has MIT licensing and
GPL-compliant Stockfish redistribution, optimistic concurrency plus idempotent
move submission, runtime-dynamic AI resilience, truthful real-engine tests,
warning-free builds enforced as errors, and SHA-pinned CI/CD to GHCR. Browser E2E
and creating the initial commit were intentionally excluded. Final local results:
85 backend tests passed with 3 explicit real-Stockfish skips, 20 frontend tests
passed, builds completed with zero warnings, dependency audits found zero known
vulnerabilities, workflows passed actionlint, and repository hygiene scans were
clean. The production Docker build remains to be executed by CI because the local
Docker daemon was unavailable.

## Deployment Plan
1. Review `git status --short --untracked-files=all`; `AGENTS.md`, the personal
   Compose override, build output, dependencies, generated frontend assets, and
   databases must remain absent from the commit.
2. Create the intended single initial commit and push `main` to the public GitHub
   repository.
3. Require the `Backend`, `Frontend`, and `Container and real Stockfish` CI jobs
   in the `main` branch ruleset before merging future changes.
4. Confirm the first CI run completes, especially the production image build and
   real Stockfish integration target that could not run locally.
5. Enable GitHub private vulnerability reporting and secret scanning/push
   protection where available.
6. Push a semantic version tag such as `v1.0.0` only after CI is green. The release
   workflow publishes `amd64` and `arm64` images to
   `ghcr.io/<owner>/<repository>` with version, latest, and immutable SHA tags,
   SBOM, and provenance.
7. In GitHub Packages, confirm once that the package is linked to the repository
   and has Public visibility. Pull and smoke-test the immutable SHA-tagged image
   before announcing the release.
