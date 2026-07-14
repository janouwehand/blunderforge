import { useState } from "react";
import type { GameStateDto, StartGameRequestDto } from "../api/gameClient";

interface Props {
  onStart: (request: StartGameRequestDto) => void;
  disabled: boolean;
  activeGame: GameStateDto | null;
  onResume: () => void;
  onDelete: () => void;
  aiConfigured: boolean;
}

export function GameSetup({ onStart, disabled, activeGame, onResume, onDelete, aiConfigured }: Props) {
  const [playerColorChoice, setPlayerColorChoice] = useState("Random");
  const [opponentElo, setOpponentElo] = useState(800);
  if (activeGame) return <section className="setup-card" aria-labelledby="active-game-title">
    <p className="eyebrow">Your board is waiting</p>
    <h2 id="active-game-title">Active game</h2>
    <p>You are playing {activeGame.settings.playerSide} against an opponent rated approximately {activeGame.settings.opponentElo} Elo.</p>
    <div className="button-row"><button className="btn-primary" onClick={onResume}>Resume game</button><button className="btn-danger" onClick={onDelete}>Delete game</button></div>
  </section>;

  return (
    <form className="game-setup setup-card" onSubmit={(event) => { event.preventDefault(); onStart({ playerColorChoice, opponentElo }); }} aria-labelledby="setup-title">
      <p className="eyebrow">One board. Clear guidance.</p>
      <h2 id="setup-title">New guided game</h2>
      <p className="muted">Choose a side and a practical opponent strength.</p>
      <fieldset>
        <legend>Play as</legend>
        <div className="radio-group">
          {["Random", "White", "Black"].map(color => <label className="radio-label" key={color}><input type="radio" name="color" value={color} checked={playerColorChoice === color} onChange={event => setPlayerColorChoice(event.target.value)} disabled={disabled} />{color}</label>)}
        </div>
      </fieldset>
      <div className="elo-heading"><label htmlFor="opponent-elo">Opponent Elo</label><details><summary aria-label="About opponent Elo">i</summary><p>Elo is an approximate practical strength. Stockfish uses its native limited-strength setting from 1320 upward. Below 1320, BlunderForge selects from a broader candidate set with calibrated randomness.</p></details></div>
      <input id="opponent-elo" type="number" min={200} max={3000} step={1} value={opponentElo} onChange={event => setOpponentElo(Number(event.target.value))} disabled={disabled} />
      <label className="sr-only" htmlFor="opponent-elo-slider">Opponent Elo slider</label>
      <input id="opponent-elo-slider" type="range" min={200} max={3000} step={1} value={opponentElo} onChange={event => setOpponentElo(Number(event.target.value))} disabled={disabled} />
      <p className="ai-availability" role="status">{aiConfigured ? "AI explanations are available when you ask for coaching." : "Stockfish-only coaching is available. Configure AI Coach and provide its secret to add explanations."}</p>
      <button type="submit" className="btn-primary" disabled={disabled || !Number.isInteger(opponentElo) || opponentElo < 200 || opponentElo > 3000}>{disabled ? "Starting…" : "Start game"}</button>
    </form>
  );
}
