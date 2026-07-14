import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { vi } from "vitest";
import { GamesScreen } from "./GamesScreen";

vi.mock("./ChessBoardView", () => ({ ChessBoardView: ({ game }: { game: { currentFen: string } }) => <div aria-label="Chessboard">{game.currentFen}</div> }));

const gameId = "11111111-1111-1111-1111-111111111111";
const history = { items: [{ gameId, date: "2026-07-13T12:00:00Z", result: "BlackWin", playerColor: "White", opponentElo: 800 }], page: 1, pageSize: 25, totalCount: 1, totalPages: 1 };
const detail = { gameId, startedAt: "2026-07-13T12:00:00Z", completedAt: "2026-07-13T12:05:00Z", status: "Resigned", result: "BlackWin", playerColor: "White", opponentElo: 800, initialFen: "initial fen", moves: [{ ply: 1, side: "White", san: "e4", uci: "e2e4", fenBefore: "initial fen", fenAfter: "after e4" }], review: { gameId, result: "BlackWin", overallQuality: "Good", criticalMoves: [], wentWell: "You developed.", futureFocus: "Check threats.", usedAi: false } };

test("loads history, replays moves, and confirms permanent deletion", async () => {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (init?.method === "DELETE") return { ok: true, status: 204, json: async () => ({}) };
    if (url.includes(gameId)) return { ok: true, status: 200, json: async () => detail };
    return { ok: true, status: 200, json: async () => history };
  });
  vi.stubGlobal("fetch", fetchMock);
  const { rerender } = render(<GamesScreen aiAvailable onOpenGame={game => { window.history.pushState(null, "", `/games/${game}`); }} onCloseGame={() => { window.history.pushState(null, "", "/games"); }} />);
  await userEvent.click(await screen.findByRole("button", { name: /BlackWin/ }));
  rerender(<GamesScreen aiAvailable gameId={gameId} onOpenGame={vi.fn()} onCloseGame={vi.fn()} />);
  expect(await screen.findByRole("heading", { name: "Game review" })).toBeInTheDocument();
  expect(screen.getByRole("status")).toHaveTextContent("Position 1 of 1");
  await userEvent.click(screen.getByRole("button", { name: "Previous move" }));
  expect(screen.getByLabelText("Chessboard")).toHaveTextContent("initial fen");
  await userEvent.click(screen.getByRole("button", { name: "Delete game" }));
  expect(screen.getByRole("alertdialog", { name: "Delete historical game?" })).toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Delete permanently" }));
  await waitFor(() => expect(fetchMock).toHaveBeenCalledWith(`/api/games/${gameId}`, { method: "DELETE" }));
});

test("generates a missing review only after the explicit action", async () => {
  const missing = { ...detail, review: null };
  const generated = { ...detail.review, usedAi: true };
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    if (init?.method === "POST") return { ok: true, status: 200, json: async () => generated };
    if (String(input).includes(gameId)) return { ok: true, status: 200, json: async () => missing };
    return { ok: true, status: 200, json: async () => history };
  });
  vi.stubGlobal("fetch", fetchMock);
  render(<GamesScreen aiAvailable gameId={gameId} onOpenGame={vi.fn()} onCloseGame={vi.fn()} />);

  expect(await screen.findByRole("button", { name: "Generate AI review" })).toBeInTheDocument();
  expect(fetchMock).not.toHaveBeenCalledWith(`/api/games/${gameId}/review`, { method: "POST" });
  await userEvent.click(screen.getByRole("button", { name: "Generate AI review" }));

  expect(await screen.findByText("What went well")).toBeInTheDocument();
  expect(fetchMock).toHaveBeenCalledWith(`/api/games/${gameId}/review`, { method: "POST" });
});
