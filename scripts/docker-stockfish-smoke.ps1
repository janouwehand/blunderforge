param(
    [string] $ImageTag = "blunderforge:stockfish-smoke"
)

$ErrorActionPreference = "Stop"

docker build --tag $ImageTag .

$uciOutput = "uci`nquit`n" | docker run --rm --interactive --entrypoint /app/stockfish/stockfish $ImageTag
$uciText = $uciOutput -join [Environment]::NewLine

if ($uciText -notmatch "uciok") {
    Write-Error "Stockfish Docker smoke test failed: output did not contain 'uciok'."
}

Write-Output "Stockfish Docker smoke test passed."
