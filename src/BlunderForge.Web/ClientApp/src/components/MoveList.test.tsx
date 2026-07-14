import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { MoveList } from "./MoveList";
import type { MoveRecordDto } from "../api/gameClient";

describe("MoveList", () => {
  it("shows empty state when no moves", () => {
    render(<MoveList moves={[]} />);

    expect(screen.getByText("No moves played yet.")).toBeInTheDocument();
  });

  it("renders move pairs", () => {
    const moves: MoveRecordDto[] = [
      { ply: 1, side: "White", san: "e4", uci: "e2e4", fenBefore: "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", fenAfter: "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1" },
      { ply: 2, side: "Black", san: "e5", uci: "e7e5", fenBefore: "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1", fenAfter: "rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2" }
    ];

    render(<MoveList moves={moves} />);

    expect(screen.getByText("e4")).toBeInTheDocument();
    expect(screen.getByText("e5")).toBeInTheDocument();
    expect(screen.getByText("1.")).toBeInTheDocument();
  });
});
