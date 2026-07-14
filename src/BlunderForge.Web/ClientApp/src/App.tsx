import { useCallback, useEffect, useState, type MouseEvent } from "react";
import { completeOpponentTurn, deleteActiveGame, deleteHistoricalGame, generateGameReview, getActiveGame, getHistoricalGame, getLegalMoves, requestCoachHelp, resignGame, startGame, submitMove, takeBack, type CoachHelpDto, type GameReviewDto, type GameStateDto, type LegalMoveDto, type StartGameRequestDto } from "./api/gameClient";
import { getAiProviderSettings, type AiProviderSettingsDto } from "./api/settingsClient";
import { AiProviderSettings } from "./components/AiProviderSettings";
import { ChessBoardView } from "./components/ChessBoardView";
import { CoachPanel } from "./components/CoachPanel";
import { ConfirmDialog } from "./components/ConfirmDialog";
import { GamesScreen } from "./components/GamesScreen";
import { GameSetup } from "./components/GameSetup";
import { GameStatusText } from "./components/GameStatusText";
import { MoveList } from "./components/MoveList";
import { ReviewPanel } from "./components/ReviewPanel";
import { shouldRequestAutomaticReview } from "./reviewPolicy";
import "./styles.css";

type Screen = "play" | "games" | "ai";
interface Route { screen: Screen; gameId?: string }

function routeFromPath(pathname: string): Route {
  const parts = pathname.split("/").filter(Boolean);
  if (parts[0] === "games") return { screen: "games", gameId: parts[1] };
  if (parts[0] === "ai-coach") return { screen: "ai" };
  return { screen: "play" };
}

export function App() {
  const [route, setRoute] = useState<Route>(() => routeFromPath(window.location.pathname)); const [showGame, setShowGame] = useState(false);
  const [game, setGame] = useState<GameStateDto | null>(null); const [legalMoves, setLegalMoves] = useState<LegalMoveDto[]>([]);
  const [coach, setCoach] = useState<CoachHelpDto | null>(null); const [review, setReview] = useState<GameReviewDto | null>(null);
  const [ai, setAi] = useState<AiProviderSettingsDto | null>(null); const [useAi, setUseAi] = useState(true);
  const [busy, setBusy] = useState(false); const [movePending, setMovePending] = useState(false); const [coachBusy, setCoachBusy] = useState(false);
  const [reviewBusy, setReviewBusy] = useState(false); const [reviewError, setReviewError] = useState("");
  const [error, setError] = useState<string | null>(null); const [confirmDelete, setConfirmDelete] = useState(false);

  const refreshLegalMoves = useCallback(async (state: GameStateDto) => {
    if (state.status === "Active" && state.activeSide === state.settings.playerSide) setLegalMoves((await getLegalMoves()).moves); else setLegalMoves([]);
  }, []);
  const loadReview = useCallback(async (state: GameStateDto) => { if (state.status !== "Active") setReview((await getHistoricalGame(state.gameId)).review); }, []);
  const loadAi = useCallback(() => getAiProviderSettings().then(setAi).catch(() => setAi(null)), []);
  useEffect(() => { void loadAi(); getActiveGame().then(state => { setGame(state); return refreshLegalMoves(state); }).catch(() => undefined); }, [loadAi, refreshLegalMoves]);
  useEffect(() => {
    if (window.location.pathname === "/" || !["/play", "/games", "/ai-coach"].some(path => window.location.pathname === path || window.location.pathname.startsWith("/games/"))) {
      window.history.replaceState(null, "", "/play"); setRoute({ screen: "play" });
    }
    const onPopState = () => { const next = routeFromPath(window.location.pathname); setRoute(next); void loadAi(); if (next.screen === "play") setShowGame(false); };
    window.addEventListener("popstate", onPopState);
    return () => window.removeEventListener("popstate", onPopState);
  }, [loadAi]);
  const aiAvailable = ai?.configured === true && ai.secretAvailable;

  function navigate(path: string) { window.history.pushState(null, "", path); const next = routeFromPath(path); setRoute(next); setError(null); void loadAi(); if (next.screen === "play") setShowGame(false); }
  function follow(path: string) { return (event: MouseEvent<HTMLAnchorElement>) => { event.preventDefault(); navigate(path); }; }
  async function requestReview(state: GameStateDto, automatic: boolean) {
    if (automatic ? !shouldRequestAutomaticReview(state.status, aiAvailable, useAi) : state.status === "Active" || !aiAvailable) return;
    setReviewBusy(true); setReviewError("");
    try { setReview(await generateGameReview(state.gameId)); }
    catch (reason) { setReviewError(reason instanceof Error ? reason.message : "The AI review could not be generated."); }
    finally { setReviewBusy(false); }
  }

  async function applyState(action: () => Promise<GameStateDto>, requestEndReview = false) {
    setBusy(true); setError(null);
    try { const state = await action(); setGame(state); await refreshLegalMoves(state); if (requestEndReview) await requestReview(state, true); else await loadReview(state); }
    catch (reason) { setError(reason instanceof Error ? reason.message : "The request failed."); }
    finally { setBusy(false); }
  }
  async function handleStart(request: StartGameRequestDto) {
    setBusy(true); setError(null); setUseAi(true); setReview(null); setCoach(null);
    try { let state = await startGame(request); if (state.status === "Active" && state.activeSide !== state.settings.playerSide) state = (await completeOpponentTurn()).state; setGame(state); setShowGame(true); await refreshLegalMoves(state); await requestReview(state, true); }
    catch (reason) { setError(reason instanceof Error ? reason.message : "The game could not be started."); }
    finally { setBusy(false); }
  }
  async function handleMove(uci: string) {
    setCoach(null); setBusy(true); setMovePending(true); setError(null);
    try { const submitted = await submitMove(uci); setGame(submitted.state); const state = submitted.opponentMovePending ? (await completeOpponentTurn()).state : submitted.state; setGame(state); setMovePending(false); await refreshLegalMoves(state); await requestReview(state, true); }
    catch (reason) { setError(reason instanceof Error ? reason.message : "The move failed."); }
    finally { setMovePending(false); setBusy(false); }
  }
  const screen = route.screen;
  return <div className="app-shell"><header className="site-header"><a className="brand" href="/play" onClick={follow("/play")}><span className="brand-mark">BF</span><span>BlunderForge</span></a><nav aria-label="Primary navigation"><a href="/play" aria-current={screen === "play" ? "page" : undefined} onClick={follow("/play")}>Play</a><a href="/games" aria-current={screen === "games" ? "page" : undefined} onClick={follow("/games")}>Games</a><a href="/ai-coach" aria-current={screen === "ai" ? "page" : undefined} onClick={follow("/ai-coach")}>AI Coach</a></nav></header><main className="app-content">
    {error && <div className="error-banner" role="alert"><strong>Something went wrong.</strong><p>{error}</p></div>}
    {screen === "games" && <GamesScreen aiAvailable={aiAvailable} gameId={route.gameId} onOpenGame={gameId => navigate(`/games/${gameId}`)} onCloseGame={() => navigate("/games")} />}
    {screen === "ai" && <><p className="eyebrow">Optional explanations</p><h1>AI Coach</h1><AiProviderSettings /></>}
    {screen === "play" && !showGame && <section className="play-landing"><div className="hero-copy"><p className="eyebrow">Guided chess, forged move by move</p><h1>Play and experiment.</h1><p>Face Stockfish at a practical Elo and ask for objective help whenever it is your turn.</p></div><GameSetup onStart={handleStart} disabled={busy} activeGame={game?.status === "Active" ? game : null} onResume={() => setShowGame(true)} onDelete={() => setConfirmDelete(true)} aiConfigured={aiAvailable} /></section>}
    {screen === "play" && showGame && game && <section className="game-screen" aria-labelledby="game-title"><div className="game-heading"><div><p className="eyebrow">Guided game</p><h1 id="game-title">Your game</h1><p>{game.settings.playerSide} against approximately {game.settings.opponentElo} Elo</p></div>{game.status === "Active" && <button className="text-button" onClick={() => setShowGame(false)}>Leave board</button>}</div><GameStatusText game={game} />{movePending && <p role="status">Processing the move…</p>}<div className="game-grid"><ChessBoardView game={game} legalMoves={legalMoves} onMove={handleMove} disabled={busy} highlightedSquares={coach?.highlightSquares} /><aside className="game-sidebar"><MoveList moves={game.moves} />{game.status === "Active" && game.activeSide === game.settings.playerSide && <CoachPanel result={coach} disabled={busy} analyzing={coachBusy} aiAvailable={aiAvailable} useAi={useAi} onUseAiChange={setUseAi} onClose={() => setCoach(null)} onCoachMe={async () => { setBusy(true); setCoachBusy(true); setError(null); try { setCoach(await requestCoachHelp(aiAvailable && useAi)); } catch (reason) { setError(reason instanceof Error ? reason.message : "Coaching failed."); } finally { setCoachBusy(false); setBusy(false); } }} />}</aside></div>
      {game.status !== "Active" && <><ReviewPanel review={review} busy={reviewBusy} aiAvailable={aiAvailable} error={reviewError} onGenerate={() => { void requestReview(game, false); }} /><a className="btn-secondary" href={`/api/games/${game.gameId}/pgn`} download={`blunderforge-${game.gameId}.pgn`}>Download PGN</a></>}
      <div className="game-actions"><button onClick={() => applyState(takeBack)} disabled={busy || game.status !== "Active" || !game.moves.some(move => move.side === game.settings.playerSide)}>Take back</button><button onClick={() => applyState(() => resignGame(game.settings.playerSide), true)} disabled={busy || game.status !== "Active"}>Resign</button><button className="btn-danger" onClick={() => setConfirmDelete(true)} disabled={busy || reviewBusy}>Delete game</button></div></section>}
    {confirmDelete && <ConfirmDialog title="Delete game?" message="This permanently deletes the game and all related moves, analysis, and review." confirmLabel="Delete permanently" onCancel={() => setConfirmDelete(false)} onConfirm={async () => { setBusy(true); setError(null); try { if (game?.status === "Active") await deleteActiveGame(); else if (game) await deleteHistoricalGame(game.gameId); setGame(null); setCoach(null); setReview(null); setShowGame(false); setConfirmDelete(false); } catch (reason) { setError(reason instanceof Error ? reason.message : "Delete failed."); setConfirmDelete(false); } finally { setBusy(false); } }} />}
  </main></div>;
}

export default App;
