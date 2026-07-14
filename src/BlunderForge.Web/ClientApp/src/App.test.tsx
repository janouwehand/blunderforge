import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, vi } from "vitest";
import { App } from "./App";
import { shouldRequestAutomaticReview } from "./reviewPolicy";

vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 404, json: async () => ({ detail: "No active game exists." }) }));
beforeEach(() => window.history.replaceState(null, "", "/play"));

test("shows the simplified guided-game setup", async () => {
  render(<App />);
  expect(await screen.findByRole("heading", { name: "New guided game" })).toBeInTheDocument();
  expect(screen.getByLabelText("Opponent Elo", { selector: "input[type=number]" })).toHaveValue(800);
});

test("requests automatic reviews only for opted-in completed games with AI", () => {
  expect(shouldRequestAutomaticReview("Completed", true, true)).toBe(true);
  expect(shouldRequestAutomaticReview("Completed", true, false)).toBe(false);
  expect(shouldRequestAutomaticReview("Completed", false, true)).toBe(false);
  expect(shouldRequestAutomaticReview("Active", true, true)).toBe(false);
});

test("uses browser URLs and responds to history navigation", async () => {
  render(<App />);
  await userEvent.click(screen.getByRole("link", { name: "Games" }));
  expect(window.location.pathname).toBe("/games");
  expect(await screen.findByRole("heading", { name: "Games" })).toBeInTheDocument();

  act(() => {
    window.history.pushState(null, "", "/ai-coach");
    window.dispatchEvent(new PopStateEvent("popstate"));
  });
  expect(await screen.findByRole("heading", { name: "AI Coach" })).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "AI Coach" })).toHaveAttribute("aria-current", "page");
});
