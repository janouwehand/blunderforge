import { useCallback, useEffect, useState } from "react";
import { deleteHistoricalGame, generateGameReview, getGameHistory, getHistoricalGame, type GameHistoryPageDto, type HistoricalGameDto, type GameStateDto } from "../api/gameClient";
import { ChessBoardView } from "./ChessBoardView";
import { ConfirmDialog } from "./ConfirmDialog";
import { MoveList } from "./MoveList";
import { ReviewPanel } from "./ReviewPanel";

interface Props { aiAvailable: boolean; gameId?: string; onOpenGame: (gameId: string) => void; onCloseGame: () => void }

export function GamesScreen({ aiAvailable, gameId, onOpenGame, onCloseGame }: Props) {
  const [page, setPage] = useState(1); const [history, setHistory] = useState<GameHistoryPageDto | null>(null);
  const [detail, setDetail] = useState<HistoricalGameDto | null>(null); const [ply, setPly] = useState(0);
  const [error, setError] = useState(""); const [loading, setLoading] = useState(true); const [confirming, setConfirming] = useState(false);
  const [reviewBusy, setReviewBusy] = useState(false); const [reviewError, setReviewError] = useState("");
  const load = useCallback(async () => { setLoading(true); setError(""); try { setHistory(await getGameHistory(page)); } catch (reason) { setError(reason instanceof Error ? reason.message : "Games could not be loaded."); } finally { setLoading(false); } }, [page]);
  useEffect(() => { void load(); }, [load]);
  async function open(gameId: string) { setLoading(true); setError(""); try { const game = await getHistoricalGame(gameId); setDetail(game); setPly(game.moves.length); } catch (reason) { setError(reason instanceof Error ? reason.message : "Game could not be loaded."); } finally { setLoading(false); } }
  useEffect(() => { if (gameId) void open(gameId); else setDetail(null); }, [gameId]);
  async function generateReview() {
    if (!detail) return;
    setReviewBusy(true); setReviewError("");
    try { setDetail({ ...detail, review: await generateGameReview(detail.gameId) }); }
    catch (reason) { setReviewError(reason instanceof Error ? reason.message : "The AI review could not be generated."); }
    finally { setReviewBusy(false); }
  }
  if (detail) {
    const fen = ply === 0 ? detail.initialFen : detail.moves[ply - 1].fenAfter;
    const replay: GameStateDto = { gameId: detail.gameId, settings: { playerColorChoice: detail.playerColor, playerSide: detail.playerColor, opponentElo: detail.opponentElo }, status: detail.status, result: detail.result, terminationReason: "", activeSide: "White", currentFen: fen, moves: detail.moves, whiteKingInCheck: false, blackKingInCheck: false };
    return <section aria-labelledby="game-detail-title"><button className="text-button" onClick={onCloseGame}>← Back to games</button><div className="detail-heading"><div><p className="eyebrow">Stored game</p><h1 id="game-detail-title">{detail.result}</h1><p>{detail.playerColor} · {detail.opponentElo} Elo · {new Date(detail.startedAt).toLocaleDateString()}</p></div><a className="btn-secondary" href={`/api/games/${detail.gameId}/pgn`} download={`blunderforge-${detail.gameId}.pgn`}>Download PGN</a></div>
      {error && <p role="alert" className="error-banner">{error}</p>}<div className="replay-layout"><ChessBoardView game={replay} legalMoves={[]} onMove={() => undefined} disabled={false} /><aside><MoveList moves={detail.moves} currentPly={ply} /><div className="replay-controls" aria-label="Move replay controls"><button onClick={() => setPly(value => Math.max(0, value - 1))} disabled={ply === 0}>Previous move</button><span role="status">Position {ply} of {detail.moves.length}</span><button onClick={() => setPly(value => Math.min(detail.moves.length, value + 1))} disabled={ply === detail.moves.length}>Next move</button></div></aside></div>
      <ReviewPanel review={detail.review} busy={reviewBusy} aiAvailable={aiAvailable} error={reviewError} onGenerate={generateReview} /><button className="btn-danger" onClick={() => setConfirming(true)}>Delete game</button>
      {confirming && <ConfirmDialog title="Delete historical game?" message="This permanently deletes the game, its moves, analysis, and review." confirmLabel="Delete permanently" onCancel={() => setConfirming(false)} onConfirm={async () => { try { await deleteHistoricalGame(detail.gameId); setConfirming(false); onCloseGame(); await load(); } catch (reason) { setError(reason instanceof Error ? reason.message : "Delete failed."); setConfirming(false); } }} />}
    </section>;
  }
  return <section aria-labelledby="games-title"><p className="eyebrow">Your archive</p><h1 id="games-title">Games</h1>{loading && <p role="status">Loading games…</p>}{error && <div className="error-banner" role="alert"><p>{error}</p><button onClick={load}>Try again</button></div>}{!loading && history?.items.length === 0 && <div className="empty-state"><h2>No finished games yet</h2><p>Completed and resigned games will appear here.</p></div>}{history && history.items.length > 0 && <><ul className="history-list">{history.items.map(game => <li key={game.gameId}><button onClick={() => onOpenGame(game.gameId)}><span><strong>{game.result}</strong><small>{new Date(game.date).toLocaleDateString()}</small></span><span>{game.playerColor} · {game.opponentElo} Elo</span></button></li>)}</ul><nav className="pagination" aria-label="Game history pages"><button onClick={() => setPage(value => value - 1)} disabled={page === 1}>Previous</button><span>Page {history.page} of {Math.max(history.totalPages, 1)}</span><button onClick={() => setPage(value => value + 1)} disabled={page >= history.totalPages}>Next</button></nav></>}</section>;
}
