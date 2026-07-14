import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { GameStatusText } from "./GameStatusText";
import type { GameStateDto } from "../api/gameClient";

function makeGame(overrides: Partial<GameStateDto> = {}): GameStateDto {
  return {
    gameId: "test-id",
    settings: {
      playerColorChoice: "White",
      playerSide: "White",
      opponentElo: 800
    },
    status: "Active",
    result: "None",
    terminationReason: "None",
    activeSide: "White",
    currentFen: "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
    moves: [],
    whiteKingInCheck: false,
    blackKingInCheck: false,
    ...overrides
  };
}

describe("GameStatusText", () => {
  it("shows player's turn when active", () => {
    render(<GameStatusText game={makeGame()} />);

    expect(screen.getByText("It is your turn.")).toBeInTheDocument();
    expect(screen.getByText("You are playing as White.")).toBeInTheDocument();
  });

  it("shows opponent thinking during the opponent turn", () => {
    render(<GameStatusText game={makeGame({ activeSide: "Black" })} />);

    expect(screen.getByText("Opponent is thinking...")).toBeInTheDocument();
  });

  it("shows check warning", () => {
    render(<GameStatusText game={makeGame({ blackKingInCheck: true })} />);

    expect(screen.getByText("Black is in check.")).toBeInTheDocument();
  });

  it("shows checkmate result", () => {
    render(<GameStatusText game={makeGame({
      status: "Completed",
      result: "WhiteWin",
      terminationReason: "Checkmate"
    })} />);

    expect(screen.getByText("White wins")).toBeInTheDocument();
    expect(screen.getByText("Checkmate")).toBeInTheDocument();
  });

  it("shows draw result", () => {
    render(<GameStatusText game={makeGame({
      status: "Completed",
      result: "Draw",
      terminationReason: "Stalemate"
    })} />);

    expect(screen.getByText("Draw")).toBeInTheDocument();
    expect(screen.getByText("Stalemate")).toBeInTheDocument();
  });
});
