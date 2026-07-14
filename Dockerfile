FROM node:24-bookworm-slim AS client-build
WORKDIR /src/src/BlunderForge.Web/ClientApp
COPY src/BlunderForge.Web/ClientApp/package*.json ./
RUN npm ci
COPY src/BlunderForge.Web/ClientApp ./
RUN npm run build

FROM debian:bookworm-slim AS stockfish-build
ARG STOCKFISH_VERSION=sf_17.1
ARG STOCKFISH_COMMIT=03e27488f3d21d8ff4dbf3065603afa21dbd0ef3
ARG TARGETARCH
WORKDIR /src
RUN apt-get update \
    && apt-get install --no-install-recommends --yes ca-certificates curl g++ git make \
    && rm -rf /var/lib/apt/lists/*
RUN git clone --branch "${STOCKFISH_VERSION}" --depth 1 https://github.com/official-stockfish/Stockfish.git stockfish
WORKDIR /src/stockfish/src
RUN set -eux; \
    test "$(git -C /src/stockfish rev-parse HEAD)" = "${STOCKFISH_COMMIT}"; \
    case "${TARGETARCH:-amd64}" in \
      amd64) stockfish_arch=x86-64 ;; \
      arm64) stockfish_arch=armv8 ;; \
      *) echo "Unsupported target architecture: ${TARGETARCH}" >&2; exit 1 ;; \
    esac; \
    make -j"$(nproc)" build ARCH="${stockfish_arch}"; \
    mkdir -p /out; \
    install -m 0755 stockfish /out/stockfish; \
    mkdir -p /out/source; \
    git -C /src/stockfish archive HEAD | tar -x -C /out/source; \
    printf '%s\n' "${STOCKFISH_COMMIT}" > /out/SOURCE_REVISION

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY BlunderForge.sln ./
COPY Directory.Build.props ./
COPY src/BlunderForge.Domain/BlunderForge.Domain.csproj src/BlunderForge.Domain/
COPY src/BlunderForge.Application/BlunderForge.Application.csproj src/BlunderForge.Application/
COPY src/BlunderForge.Infrastructure/BlunderForge.Infrastructure.csproj src/BlunderForge.Infrastructure/
COPY src/BlunderForge.Web/BlunderForge.Web.csproj src/BlunderForge.Web/
COPY tests/BlunderForge.DomainTests/BlunderForge.DomainTests.csproj tests/BlunderForge.DomainTests/
COPY tests/BlunderForge.ApplicationTests/BlunderForge.ApplicationTests.csproj tests/BlunderForge.ApplicationTests/
COPY tests/BlunderForge.WebTests/BlunderForge.WebTests.csproj tests/BlunderForge.WebTests/
COPY tests/BlunderForge.InfrastructureTests/BlunderForge.InfrastructureTests.csproj tests/BlunderForge.InfrastructureTests/
COPY tests/BlunderForge.FakeStockfish/BlunderForge.FakeStockfish.csproj tests/BlunderForge.FakeStockfish/
RUN dotnet restore BlunderForge.sln
COPY . .
COPY --from=client-build /src/src/BlunderForge.Web/wwwroot ./src/BlunderForge.Web/wwwroot
RUN dotnet publish src/BlunderForge.Web/BlunderForge.Web.csproj --configuration Release --output /app/publish --no-restore

FROM build AS stockfish-integration-tests
COPY --from=stockfish-build /out/stockfish /app/stockfish/stockfish
ENV BLUNDERFORGE_TEST_STOCKFISH_PATH=/app/stockfish/stockfish \
    BlunderForge__Stockfish__Path=/app/stockfish/stockfish
RUN dotnet test BlunderForge.sln --no-restore --filter Stockfish

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
RUN apt-get update \
    && apt-get install --no-install-recommends --yes curl libstdc++6 \
    && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /app/stockfish /workspace/.data
COPY --from=stockfish-build /out/stockfish /app/stockfish/stockfish
ENV BlunderForge__Stockfish__Path=/app/stockfish/stockfish
WORKDIR /workspace

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update \
    && apt-get install --no-install-recommends --yes curl libstdc++6 \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd --system blunderforge \
    && useradd --system --gid blunderforge --home-dir /app blunderforge \
    && mkdir -p /app/data /app/stockfish \
    && chown -R blunderforge:blunderforge /app

WORKDIR /app
COPY --from=build --chown=blunderforge:blunderforge /app/publish .
COPY --from=stockfish-build --chown=blunderforge:blunderforge /out/stockfish /app/stockfish/stockfish
COPY --from=stockfish-build /out/source /usr/share/stockfish/source
COPY --from=stockfish-build /out/SOURCE_REVISION /usr/share/stockfish/SOURCE_REVISION
COPY --from=stockfish-build /out/source/Copying.txt /usr/share/stockfish/COPYING.txt
COPY LICENSE THIRD_PARTY_NOTICES.md /usr/share/doc/blunderforge/

LABEL org.opencontainers.image.title="BlunderForge" \
      org.opencontainers.image.licenses="MIT AND GPL-3.0-or-later" \
      org.opencontainers.image.description="Local guided chess application with Stockfish coaching"

ENV ASPNETCORE_URLS=http://+:8080 \
    BlunderForge__DataDirectory=/app/data \
    BlunderForge__Stockfish__Path=/app/stockfish/stockfish

VOLUME ["/app/data"]
EXPOSE 8080
USER blunderforge
STOPSIGNAL SIGTERM

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl --fail http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "BlunderForge.Web.dll"]
