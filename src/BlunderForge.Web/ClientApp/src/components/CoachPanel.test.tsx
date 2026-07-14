import { fireEvent, render, screen } from "@testing-library/react";
import { vi } from "vitest";
import { CoachPanel } from "./CoachPanel";

test("shows deterministic Stockfish text and requests coaching", () => {
  const onCoachMe = vi.fn();
  render(<CoachPanel disabled={false} analyzing={false} aiAvailable={false} useAi={false} onUseAiChange={vi.fn()} onClose={vi.fn()} onCoachMe={onCoachMe} result={{ recommendedMove: "e4", recommendedMoveUci: "e2e4", textAlternative: "Stockfish recommends e4.", highlightSquares: ["e2", "e4"], arrow: { from: "e2", to: "e4" }, hint: null, explanation: null, aiStatus: null }} />);
  expect(screen.getByText("Stockfish recommends e4.")).toBeInTheDocument();
  expect(screen.getByText(/Arrow: e2 to e4/)).toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: "Coach me" }));
  expect(onCoachMe).toHaveBeenCalledOnce();
});

test("offers AI opt-in only when available and lets the result close", () => {
  const onClose = vi.fn(); const onUseAiChange = vi.fn();
  render(<CoachPanel disabled={false} analyzing={false} aiAvailable useAi onUseAiChange={onUseAiChange} onClose={onClose} onCoachMe={vi.fn()} result={{ recommendedMove: "e4", recommendedMoveUci: "e2e4", textAlternative: "Stockfish recommends e4.", highlightSquares: ["e2", "e4"], arrow: { from: "e2", to: "e4" }, hint: "Claim the center.", explanation: "This opens lines.", aiStatus: null }} />);
  expect(screen.getByRole("checkbox", { name: "Use AI explanation and game review" })).toBeChecked();
  fireEvent.click(screen.getByRole("button", { name: "Close coach result" }));
  expect(onClose).toHaveBeenCalledOnce();
});

test("shows Stockfish progress only for a coaching operation", () => {
  const { rerender } = render(<CoachPanel disabled analyzing={false} aiAvailable={false} useAi={false} onUseAiChange={vi.fn()} onClose={vi.fn()} onCoachMe={vi.fn()} result={null} />);
  expect(screen.queryByText("Stockfish is analyzing the position…")).not.toBeInTheDocument();

  rerender(<CoachPanel disabled analyzing aiAvailable={false} useAi={false} onUseAiChange={vi.fn()} onClose={vi.fn()} onCoachMe={vi.fn()} result={null} />);
  expect(screen.getByText("Stockfish is analyzing the position…")).toBeInTheDocument();
});
