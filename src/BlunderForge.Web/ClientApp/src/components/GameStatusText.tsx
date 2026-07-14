import type { GameStateDto } from "../api/gameClient";

interface GameStatusTextProps {
  game: GameStateDto;
}

function describeResult(result: string): string {
  switch (result) {
    case "WhiteWin": return "White wins";
    case "BlackWin": return "Black wins";
    case "Draw": return "Draw";
    default: return "No result";
  }
}

function describeTermination(reason: string): string {
  switch (reason) {
    case "Checkmate": return "Checkmate";
    case "Stalemate": return "Stalemate";
    case "ThreefoldRepetition": return "Threefold repetition";
    case "FiftyMoveRule": return "Fifty-move rule";
    case "InsufficientMaterial": return "Insufficient material";
    case "Resignation": return "Resignation";
    default: return "";
  }
}

function describeCheck(game: GameStateDto): string {
  if (game.status !== "Active") return "";
  if (game.whiteKingInCheck) return "White is in check.";
  if (game.blackKingInCheck) return "Black is in check.";
  return "";
}

export function GameStatusText({ game }: GameStatusTextProps) {
  const playerColor = game.settings.playerSide === "White" ? "White" : "Black";

  if (game.status === "Active") {
    const turnText = game.activeSide === game.settings.playerSide
      ? "It is your turn."
      : "Opponent is thinking...";
    const checkText = describeCheck(game);

    return (
      <div className="game-status" role="status" aria-live="polite">
        <p className="game-status-turn">{turnText}</p>
        {checkText && <p className="game-status-check" role="alert">{checkText}</p>}
        <p className="game-status-player">You are playing as {playerColor}.</p>
      </div>
    );
  }

  if (game.status === "Resigned") {
    return (
      <div className="game-status" role="status" aria-live="polite">
        <p className="game-status-result">{describeResult(game.result)}</p>
        <p className="game-status-reason">by resignation</p>
      </div>
    );
  }

  if (game.status === "Completed") {
    const result = describeResult(game.result);
    const reason = describeTermination(game.terminationReason);

    return (
      <div className="game-status" role="status" aria-live="polite">
        <p className="game-status-result">{result}</p>
        {reason && <p className="game-status-reason">{reason}</p>}
      </div>
    );
  }

  return (
    <div className="game-status" role="status" aria-live="polite">
      <p className="game-status-result">Game ended</p>
    </div>
  );
}
