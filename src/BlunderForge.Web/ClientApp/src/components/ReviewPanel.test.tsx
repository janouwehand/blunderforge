import { render, screen } from "@testing-library/react";
import { ReviewPanel } from "./ReviewPanel";

test("shows dedicated AI review progress", () => {
  render(<ReviewPanel review={null} busy aiAvailable onGenerate={() => undefined} />);
  expect(screen.getByRole("status")).toHaveTextContent("AI is generating your game review…");
  expect(screen.queryByText("Stockfish is analyzing the position…")).not.toBeInTheDocument();
});

test("does not offer generation without configured AI", () => {
  render(<ReviewPanel review={null} aiAvailable={false} onGenerate={() => undefined} />);
  expect(screen.queryByRole("button", { name: "Generate AI review" })).not.toBeInTheDocument();
  expect(screen.getByText(/Configure AI Coach/)).toBeInTheDocument();
});
