import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { vi } from "vitest";
import { GameSetup } from "./GameSetup";

test("starts the single guided game with default Elo", async () => {
  const onStart = vi.fn();
  render(<GameSetup onStart={onStart} disabled={false} activeGame={null} onResume={vi.fn()} onDelete={vi.fn()} aiConfigured={false} />);
  expect(screen.getByRole("heading", { name: "New guided game" })).toBeInTheDocument();
  expect(screen.getByLabelText("Opponent Elo", { selector: "input[type=number]" })).toHaveValue(800);
  await userEvent.click(screen.getByRole("button", { name: "Start game" }));
  expect(onStart).toHaveBeenCalledWith({ playerColorChoice: "Random", opponentElo: 800 });
});

test("keeps Elo controls synchronized and explains the hybrid range", async () => {
  render(<GameSetup onStart={vi.fn()} disabled={false} activeGame={null} onResume={vi.fn()} onDelete={vi.fn()} aiConfigured={false} />);
  const slider = screen.getByLabelText("Opponent Elo slider");
  await userEvent.clear(screen.getByLabelText("Opponent Elo", { selector: "input[type=number]" }));
  await userEvent.type(screen.getByLabelText("Opponent Elo", { selector: "input[type=number]" }), "1320");
  expect(slider).toHaveValue("1320");
  await userEvent.click(screen.getByLabelText("About opponent Elo"));
  expect(screen.getByText(/native limited-strength setting from 1320/)).toBeVisible();
  expect(screen.getByText(/Stockfish-only coaching is available/)).toHaveAttribute("role", "status");
});
